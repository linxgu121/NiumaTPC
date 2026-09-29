using System;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 一位参与者在一场对局中的选角状态
    /// 不读取大厅选择，不负责 UI、网络通信或角色生成
    /// </summary>
    public class CharacterSelectionSession
    {
        private readonly ICharacterCatalogQuery _catalogQuery;

        /// <summary>
        /// 所属对局的标识
        /// 只用于关联会话，本身不代表网络身份或操作权限
        /// </summary>
        public string MatchId { get; }

        /// <summary>
        /// 当前选角阶段，只能由本类修改
        /// </summary>
        public MatchCharacterSelectionType Phase { get; private set; }

        /// <summary>
        /// 当前候选角色 null 表示尚未选择。
        /// 锁定后保留该值，但生成器必须通过锁定查询接口读取
        /// </summary>
        public ushort? CandidateCharacterId { get; private set; }

        public bool IsLocked => Phase == MatchCharacterSelectionType.Locked;

        /// <summary>
        /// 从已有会话数据派生，不另外保存一份可修改的选择状态。
        /// Closed 时候选已清空，但不代表会话重新开放。
        /// </summary>
        public PlayerCharacterSelectionState SelectionState
        {
            get
            {
                if (IsLocked)
                {
                    return PlayerCharacterSelectionState.Confirmed;
                }
                    

                return CandidateCharacterId.HasValue
                    ? PlayerCharacterSelectionState.Selected
                    : PlayerCharacterSelectionState.Unselected;
            }
        }


        public CharacterSelectionSession(
            string matchId,
            ICharacterCatalogQuery catalogQuery)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("选角会话必须提供所属对局的 MatchId", nameof(matchId));
            }

            _catalogQuery = catalogQuery
                ?? throw new ArgumentNullException(nameof(catalogQuery));

            MatchId = matchId.Trim();
            Phase = MatchCharacterSelectionType.Selecting;

            // 每局从未选状态开始，不继承大厅或上一局的选择。
            CandidateCharacterId = null;
        }

        /// <summary>
        /// 修改本局候选。
        /// 失败时保留原候选和原阶段。
        /// </summary>
        public bool TrySelect(ushort characterId, out string error)
        {
            error = string.Empty;

            if (Phase == MatchCharacterSelectionType.Closed)
            {
                error = "本次选角会话已经结束。";
                return false;
            }

            if (IsLocked)
            {
                error = "本局角色已经确认，不能更换。";
                return false;
            }

            if (!IsKnownCharacter(characterId))
            {
                error = $"无法选择角色 {characterId}：角色不存在或目录尚未就绪。";
                return false;
            }

            CandidateCharacterId = characterId;
            return true;
        }

        /// <summary>
        /// 确认当前候选，并锁定本局角色
        /// 重复确认返回成功，但不会再次修改状态
        /// 返回成功不等于允许再次生成角色
        /// </summary>
        public bool TryConfirm(out string error)
        {
            error = string.Empty;

            if (Phase == MatchCharacterSelectionType.Closed)
            {
                error = "本次选角会话已经结束。";
                return false;
            }

            // 幂等确认：已经锁定时保持原结果。
            if (IsLocked)
            {
                return true;
            }


            if (!CandidateCharacterId.HasValue)
            {
                error = "请先选择本局使用的角色。";
                return false;
            }

            ushort characterId = CandidateCharacterId.Value;

            // 选择与确认之间，目录服务可能已经停止或发生变化。
            // 确认失败不擅自清空或替换原候选。
            if (!IsKnownCharacter(characterId))
            {
                error = $"无法确认角色 {characterId}：角色不存在或目录尚未就绪。";
                return false;
            }

            Phase = MatchCharacterSelectionType.Locked;
            return true;
        }

        /// <summary>
        /// 只有已锁定的角色才可以交给后续生成流程。
        /// 返回 false 时，调用方不能使用输出的角色 ID。
        /// </summary>
        public bool TryGetLockedCharacterId(out ushort characterId)
        {
            characterId = default;

            if (!IsLocked || !CandidateCharacterId.HasValue)
                return false;

            characterId = CandidateCharacterId.Value;
            return true;
        }

        /// <summary>
        /// 结束本次选角会话，重复调用没有额外影响。
        /// 只在取消入局或离开对局时调用，不用于临时隐藏 UI。
        /// </summary>
        public void Close()
        {
            if (Phase == MatchCharacterSelectionType.Closed)
            {
                return;
            }


            CandidateCharacterId = null;
            Phase = MatchCharacterSelectionType.Closed;
        }

        //TODO:
        // 增加一些默认规则
        // 当倒计时结束后未选择角色将从第一个角色开始选，如果第一个角色有人使用则下一个
        // (这里需要一个开关确定是否可以重复选择角色，如果开启角色不能重复，关闭则可以重复选择，默认不选择使用第一个)

        /// <summary>
        /// 目录校验
        /// </summary>
        /// <param name="characterId"></param>
        /// <returns></returns>
        private bool IsKnownCharacter(ushort characterId)
        {
            return _catalogQuery.TryGetDefinition(
                characterId,
                out CharacterDefinitionSO definition)
                && definition != null;
        }


    }
}
