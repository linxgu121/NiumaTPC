using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Character.Motion.MotionEnums
{
    /// <summary>
    /// 动画驱动类型
    /// </summary>
    [Serializable]
    public enum MotionType
    {
        /// <summary>
        /// 玩家控制
        /// </summary>
        InputDriven,
        /// <summary>
        /// 动画控制
        /// </summary>
        CurveDriven,
        /// <summary>
        /// 混合控制(自己+动画)
        /// </summary>
        Mixed
    }
}
