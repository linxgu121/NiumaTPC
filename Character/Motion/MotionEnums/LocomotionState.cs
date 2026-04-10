using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Item.Motion.MotionEnums
{
    /// <summary>
    /// 下半身的运动状态分类 控制动画混合树的输入源
    /// </summary>
    public enum LocomotionState
    {
        /// <summary>
        /// 待机
        /// </summary>
        Idle = 0,
        /// <summary>
        /// 走
        /// </summary>
        Walk = 1,
        /// <summary>
        /// 慢跑
        /// </summary>
        Jog = 2,
        /// <summary>
        /// 冲刺
        /// </summary>
        Sprint = 3,
        /// <summary>
        /// 下蹲
        /// </summary>
        Crouch = 4
    }
}