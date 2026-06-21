using System;
using UnityEngine;

namespace NiumaTPC.CombatReaction
{
    [Serializable]
    public struct TPCCombatReactionRequest
    {
        [Tooltip("请求编号。由 CombatBridge 或调用方填写，用于日志追踪；可以为空。")]
        public string RequestId;

        [InspectorName("表现类型")]
        [Tooltip("要播放的战斗表现类型。")]
        public TPCCombatReactionType ReactionType;

        [InspectorName("命中点")]
        [Tooltip("命中点世界坐标。第一版 TPC 仅保留给后续受击转向、特效和镜头使用。")]
        public Vector3 HitPoint;

        [InspectorName("受击方向")]
        [Tooltip("受击方向。第一版 TPC 仅保留给后续受击朝向、击退和动画选择使用。")]
        public Vector3 HitDirection;

        [InspectorName("力度")]
        [Tooltip("表现力度。第一版 TPC 仅保留给后续击退、震屏或动画分级使用。")]
        public float Force;

        [InspectorName("硬直时间")]
        [Tooltip("Combat Reaction 传入的硬直秒数。第一版 TPC 仅保留该数据，后续可由受击状态或表现桥接消费。")]
        public float StaggerSeconds;

        [InspectorName("击退距离")]
        [Tooltip("Combat Reaction 传入的击退距离。第一版 TPC 仅保留该数据，后续可用于位移表现或 Motion Warping。")]
        public float KnockbackDistance;

        [InspectorName("受击特效 CueId")]
        [Tooltip("Combat Reaction 传入的受击特效 ID。第一版 TPC 不直接播放特效，可由后续 VFX 桥接读取。")]
        public string HitVfxCueId;

        [InspectorName("受击音效 CueId")]
        [Tooltip("Combat Reaction 传入的受击音效 ID。第一版 TPC 不直接播放音效，可由后续 Audio 桥接读取。")]
        public string HitAudioCueId;

        [InspectorName("覆盖优先级")]
        [Tooltip("大于 0 时覆盖配置资产里的动作优先级；小于等于 0 时使用配置资产。")]
        public int PriorityOverride;

        [InspectorName("覆盖冻结输入")]
        [Tooltip("勾选后使用下方“冻结输入”的值；不勾选时使用配置资产里的设置。")]
        public bool OverrideBlockInput;

        [InspectorName("冻结输入")]
        [Tooltip("覆盖冻结输入时生效。true 表示播放表现期间冻结玩家输入。")]
        public bool BlockInput;

        [InspectorName("覆盖应用重力")]
        [Tooltip("勾选后使用下方“应用重力”的值；不勾选时使用配置资产里的设置。")]
        public bool OverrideApplyGravity;

        [InspectorName("应用重力")]
        [Tooltip("覆盖应用重力时生效。true 表示 Override 动作期间继续应用重力。")]
        public bool ApplyGravity;

        [InspectorName("覆盖立即仲裁")]
        [Tooltip("勾选后使用下方“立即仲裁”的值；不勾选时使用配置资产里的设置。")]
        public bool OverrideFlushImmediately;

        [InspectorName("立即仲裁")]
        [Tooltip("覆盖立即仲裁时生效。true 表示提交动作请求后本帧立即执行 ActionArbiter。")]
        public bool FlushImmediately;

        public static TPCCombatReactionRequest ForType(TPCCombatReactionType reactionType, string requestId = null)
        {
            return new TPCCombatReactionRequest
            {
                RequestId = requestId,
                ReactionType = reactionType
            };
        }
    }
}
