using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Character.Motion.MotionEnums
{
    /// <summary>
    /// 离散化的角色意图方向（8方向） 
    /// 这是将连续的摇杆输入量化成8个离散方向 用于选择对应的启动动画与根运动方向
    /// </summary>
    public enum DesiredDirection
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 前
        /// </summary>
        Forward,
        /// <summary>
        /// 后
        /// </summary>
        Backward,
        /// <summary>
        /// 左
        /// </summary>
        Left,
        /// <summary>
        /// 右
        /// </summary>
        Right,
        /// <summary>
        /// 左前方
        /// </summary>
        ForwardLeft,
        /// <summary>
        /// 右前方
        /// </summary>
        ForwardRight,
        /// <summary>
        /// 左后方
        /// </summary>
        BackwardLeft,
        /// <summary>
        /// 右后方
        /// </summary>
        BackwardRight
    }
}
