

using System;
using System.Collections.Generic;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 一支队伍在一场对局中的选角协调器。
    /// 不负责 UI、真实倒计时、网络通信或角色生成。
    /// 所有修改应在同一执行线程串行调用。
    /// </summary>
    public class TeamCharacterSelectionCoordinator
    {
        private readonly Dictionary<int, CharacterSelectionSession> _sessions =new Dictionary<int, CharacterSelectionSession>();

        // 输入列表的顺序就是固定队伍席位顺序，不按玩家编号排序
        private readonly List<int> _participantOrder = new List<int>();

        private readonly List<ushort> _characterOrder = new List<ushort>();

        private readonly HashSet<ushort> _allowedCharacterIds = new HashSet<ushort>();

        // 保存当前候选被接受的顺序，不使用客户端时间。
        private readonly Dictionary<int, ulong> _candidateSequences = new Dictionary<int, ulong>();

        private ulong _lastCandidateSequence;
        private IReadOnlyList<int> _timeoutFailures = Array.Empty<int>();

        public string MatchId { get; }

        /// <summary>
        /// true：同队已确认角色不能重复。
        /// false：允许多位队员确认同一角色。
        /// </summary>
        public bool PreventDuplicateCharacters { get; }

        public bool HasTimedOut { get; private set; }

        public bool IsClosed { get; private set; }

        #region Initialization(初始化)

        /// <param name="participantIds">
        /// 按固定队伍席位排列的局内参与者编号，不是身份认证凭据。
        /// </param>
        /// <param name="orderedCharacterIds">
        /// 本次允许选择的角色，顺序同时决定默认补选优先级。
        /// </param>
        public TeamCharacterSelectionCoordinator(
            string matchId,
            ICharacterCatalogQuery catalogQuery,
            IReadOnlyList<int> participantIds,
            IReadOnlyList<ushort> orderedCharacterIds,
            bool preventDuplicateCharacters = true)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("必须提供对局编号", nameof(matchId));

            if (catalogQuery == null)
                throw new ArgumentNullException(nameof(catalogQuery));

            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("队伍参与者不能为空", nameof(participantIds));

            if (orderedCharacterIds == null || orderedCharacterIds.Count == 0)
                throw new ArgumentException("可选角色不能为空", nameof(orderedCharacterIds));

            MatchId = matchId.Trim();
            PreventDuplicateCharacters = preventDuplicateCharacters;

            // 复制配置，避免调用方之后修改列表影响本次选角
            foreach (ushort characterId in orderedCharacterIds)
            {
                if (!_allowedCharacterIds.Add(characterId))
                {
                    throw new ArgumentException(
                        $"可选角色编号重复：{characterId}",
                        nameof(orderedCharacterIds));
                }

                _characterOrder.Add(characterId);
            }

            foreach (int participantId in participantIds)
            {
                if (_sessions.ContainsKey(participantId))
                {
                    throw new ArgumentException(
                        $"参与者编号重复：{participantId}。",
                        nameof(participantIds));
                }

                _participantOrder.Add(participantId);
                _candidateSequences.Add(participantId, 0);

                _sessions.Add(
                    participantId,
                    new CharacterSelectionSession(MatchId, catalogQuery));
            }
        }

        #endregion

        /// <summary>
        /// 提供会话供外部读取。
        /// 修改统一经过协调器，不直接调用会话内部修改方法。
        /// </summary>
        public bool TryGetSession(
            int participantId,
            out CharacterSelectionSession session)
        {
            return _sessions.TryGetValue(participantId, out session);
        }

        /// <summary>
        /// 选择角色
        /// </summary>
        public bool TrySelect(
            int participantId,
            ushort characterId,
            out string error)
        {
            if (!TryGetWritableSession(participantId, out var session, out error))
                return false;

            if (HasTimedOut)
            {
                error = "选角时间已结束，不能修改候选";
                return false;
            }

            if (!_allowedCharacterIds.Contains(characterId))
            {
                error = $"角色 {characterId} 不在本次可选列表中";
                return false;
            }

            bool candidateChanged = session.CandidateCharacterId != characterId;

            if (candidateChanged && _lastCandidateSequence == ulong.MaxValue)
            {
                error = "候选顺序编号已耗尽，无法接受新的选择";
                return false;
            }

            // 会话负责阶段和目录校验，失败时不能更新顺序
            if (!session.TrySelect(characterId, out error))
                return false;

            if (candidateChanged)
            {
                _lastCandidateSequence++;
                _candidateSequences[participantId] = _lastCandidateSequence;
            }

            // 重复点击同一候选不刷新顺序
            // 候选不占用角色，所以这里不检查队友的锁定结果
            return true;
        }

        /// <summary>
        /// 确认选角
        /// </summary>
        public bool TryConfirm(int participantId, out string error)
        {
            if (!TryGetWritableSession(participantId, out var session, out error))
                return false;

            // 已确认者重复请求仍返回原结果。
            // 即使已经超时，也不会重新确认或改变角色。
            if (session.IsLocked)
                return true;

            if (HasTimedOut)
            {
                error = "选角时间已结束，不能手动确认。";
                return false;
            }

            return TryLockCandidate(participantId, out error);
        }

        #region Timeout Dispose(选角超时处理)

        /// <summary>
        /// 选角超时处理
        /// 
        /// 由外部编排器在截止时调用
        /// 返回 false 时，通过未分配名单决定后续流程
        /// 重复调用返回第一次结算结果，不重新分配
        /// </summary>
        public bool TryResolveTimeout(
            out IReadOnlyList<int> unassignedParticipantIds,
            out string error)
        {
            unassignedParticipantIds = Array.Empty<int>();
            error = string.Empty;

            if (IsClosed)
            {
                error = "队伍选角会话已经结束。";
                return false;
            }

            if (!HasTimedOut)
            {
                // 先关掉手动入口，再执行内部结算。
                HasTimedOut = true;

                // 第一轮：先处理所有已有候选，不在这里补选。
                ResolvePendingCandidates();

                // 第二轮：按固定队伍席位给剩余成员补选。
                var failures = new List<int>();

                foreach (int participantId in _participantOrder)
                {
                    CharacterSelectionSession session = _sessions[participantId];

                    if (session.IsLocked)
                        continue;

                    if (session.Phase == MatchCharacterSelectionType.Selecting &&
                        TryAssignFirstAvailable(participantId))
                    {
                        continue;
                    }

                    // 角色不足或没有合法角色时明确失败，不强制重复。
                    // 已关闭会话也不会被重新打开。
                    session.Close();
                    failures.Add(participantId);
                }

                _timeoutFailures = failures.AsReadOnly();
            }

            unassignedParticipantIds = _timeoutFailures;

            if (_timeoutFailures.Count > 0)
            {
                error = $"有 {_timeoutFailures.Count} 位参与者未能分配角色。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 超时结算
        /// 
        /// 处理已经预选了候选角色，但还没点确认的玩家
        /// </summary>
        private void ResolvePendingCandidates()
        {
            var candidates = new List<int>();

            foreach (int participantId in _participantOrder)
            {
                CharacterSelectionSession session = _sessions[participantId];

                if (session.Phase == MatchCharacterSelectionType.Selecting &&
                    session.CandidateCharacterId.HasValue)
                {
                    candidates.Add(participantId);
                }
            }

            // 使用候选接受顺序，不使用玩家编号或字典遍历顺序
            candidates.Sort((left, right) =>
                _candidateSequences[left].CompareTo(_candidateSequences[right]));

            foreach (int participantId in candidates)
            {
                // 确认成功立即形成占用
                // 冲突或失效者暂不关闭，留给第二轮补选
                TryLockCandidate(participantId, out _);
            }
        }

        /// <summary>
        /// 超时结算
        /// 
        /// 对没有选择的玩家进行处理
        /// 按预设角色顺序依次遍历，找到第一个没被别人占用的角色，选中并锁定
        /// </summary>
        private bool TryAssignFirstAvailable(int participantId)
        {
            CharacterSelectionSession session = _sessions[participantId];

            foreach (ushort characterId in _characterOrder)
            {
                if (IsOccupiedByOther(participantId, characterId))
                    continue;

                // 内部补选不调用公开 TrySelect：
                // 手动入口已经关闭，且自动补选不参与候选先后竞争。
                if (!session.TrySelect(characterId, out _))
                    continue;

                if (TryLockCandidate(participantId, out _))
                    return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// 取消入局或离开对局时关闭。
        /// 临时隐藏选角 UI 不应调用。
        /// </summary>
        public void Close()
        {
            if (IsClosed)
            {
                return;
            }
                

            IsClosed = true;

            foreach (int participantId in _participantOrder)
            {
                _sessions[participantId].Close();
            }
                
        }
         
        #region Help(辅助校验方法)

        /// <summary>
        /// 校验 全局会话未关闭 + 参与者存在 + 该玩家个人会话未关闭
        /// </summary>
        private bool TryGetWritableSession(
            int participantId,
            out CharacterSelectionSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            if (IsClosed)
            {
                error = "队伍选角会话已经结束。";
                return false;
            }

            if (!_sessions.TryGetValue(participantId, out session))
            {
                error = $"参与者 {participantId} 不属于本队。";
                return false;
            }

            if (session.Phase == MatchCharacterSelectionType.Closed)
            {
                error = "该参与者的选角会话已经结束。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 校验玩家预选角色的合法性、是否被别人占用
        /// </summary>
        private bool TryLockCandidate(int participantId, out string error)
        {
            error = string.Empty;
            CharacterSelectionSession session = _sessions[participantId];

            if (session.Phase != MatchCharacterSelectionType.Selecting)
            {
                error = "该参与者当前不处于可确认阶段";
                return false;
            }

            if (!session.CandidateCharacterId.HasValue)
            {
                error = "请先选择本局使用的角色";
                return false;
            }

            ushort characterId = session.CandidateCharacterId.Value;

            if (!_allowedCharacterIds.Contains(characterId))
            {
                error = $"角色 {characterId} 不在本次可选列表中";
                return false;
            }

            if (IsOccupiedByOther(participantId, characterId))
            {
                error = $"角色 {characterId} 已被同队其他玩家确认";
                return false;
            }

            // 再由会话校验目录，成功才进入 Locked。
            return session.TryConfirm(out error);
        }
        
        /// <summary>
        /// 检查指定角色，是否已经被其他玩家成功锁定占用
        /// </summary>
        private bool IsOccupiedByOther(int participantId, ushort characterId)
        {
            if (!PreventDuplicateCharacters)
            {
                return false;
            }
                

            foreach (int otherParticipantId in _participantOrder)
            {
                if (otherParticipantId == participantId)
                {
                    continue;
                }
                    
                CharacterSelectionSession other = _sessions[otherParticipantId];

                if (other.TryGetLockedCharacterId(out ushort lockedCharacterId) &&
                    lockedCharacterId == characterId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

    }
}
