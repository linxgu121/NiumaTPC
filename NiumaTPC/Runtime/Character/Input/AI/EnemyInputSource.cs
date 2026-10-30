using NiumaTPC.Character.Input.Base;
using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Enemy.Brain;
using NiumaTPC.Enemy.Core;
using UnityEngine;

namespace NiumaTPC.Character.Input.AI
{
    /// <summary>
    /// 将服务器AI控制意图转换为TPC原始输入
    /// 不负责选敌、寻路、移动模拟或网络同步
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyInputSource : InputSourceBase
    {
        #region Inspector(面板配置)

        [SerializeField]
        [Tooltip("负责生成AI控制意图的EnemyBrain")]
        private EnemyBrain brain;

        #endregion

        #region Runtime State(运行时状态)

        private NiumaCharacterController _character;

        #endregion

        #region Public API(公共接口)

        public EnemyBrain Brain => brain;

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        private void Reset()
        {
            brain = GetComponentInParent<EnemyBrain>();

            // AI意图本身会持续保存，不需要玩家硬件输入的短时防抖
            InputFlickerBuffer = 0f;
        }

        protected override void Awake()
        {
            base.Awake();

            _character = GetComponentInParent<NiumaCharacterController>();

            if (brain == null)
            {
                brain = GetComponentInParent<EnemyBrain>();
            }
        }

        #endregion

        #region Input Sampling(输入采样)

        /// <summary>
        /// 将当前AI控制意图写入TPC原始输入快照
        /// 只有服务器权威AI可以产生有效输入
        /// </summary>
        public override void FetchRawInput(ref RawInputData rawData)
        {
            ClearRawInput(ref rawData);

            if (brain == null || !brain.IsServerAuthorityActive || brain.RuntimeData == null)
            {
                return;
            }

            if (_runtimeData == null && _character != null)
            {
                _runtimeData = _character.RuntimeData;
            }

            if (_runtimeData == null)
            {
                return;
            }

            EnemyControlIntent intent = brain.RuntimeData.ControlIntent;

            rawData.MoveAxis = Vector2.ClampMagnitude(intent.Move, 1f);

            if (IsFinite(intent.ViewYaw))
            {
                _runtimeData.ViewYaw = Mathf.Repeat(intent.ViewYaw, 360f);
            }

            if (IsFinite(intent.ViewPitch))
            {
                _runtimeData.ViewPitch = intent.ViewPitch;
            }

            if (intent.SprintHeld)
            {
                rawData.HeldButtons |= RawInputButtons.Sprint;
            }

            /*
             * 近战系统当前约定：
             * FirePressed 对应轻击
             * AimPressed 对应重击
             */
            if (intent.LightAttackPressed)
            {
                rawData.PressedButtons |= RawInputButtons.Fire;
            }

            if (intent.HeavyAttackPressed)
            {
                rawData.PressedButtons |= RawInputButtons.Aim;
            }

            // 一次性动作进入输入缓冲后立即清除，避免每帧重复提交攻击
            brain.RuntimeData.ControlIntent.ClearTransientActions();
        }

        #endregion

        #region Helpers(辅助方法)

        private static void ClearRawInput(ref RawInputData rawData)
        {
            rawData.MoveAxis = Vector2.zero;
            rawData.LookAxis = Vector2.zero;
            rawData.HeldButtons = RawInputButtons.None;
            rawData.PressedButtons = RawInputButtons.None;
        }

        /// <summary>
        /// 检查AI朝向数值是否可以安全写入角色运行数据
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        #endregion
    }
}