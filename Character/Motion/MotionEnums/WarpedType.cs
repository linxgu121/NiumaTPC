using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Character.Motion.MotionEnums
{
    /// <summary>
    /// 扭曲动作类型，用于自动烘焙特征点
    /// </summary>
    public enum WarpedType
    {
        /// <summary>
        /// 手动模式 使用用户配置的点
        /// </summary>
        None,
        /// <summary>
        /// 自动探测Y轴极大值（顶点）
        /// </summary>
        Vault,
        /// <summary>
        /// 自动探测XZ平面最大位移点
        /// </summary>
        Dodge,
        /// <summary>
        /// 仅生成1.0的终点
        /// </summary>
        Simple,
        /// <summary>
        /// 保留用户定义的特征点 仅烘焙曲线数
        /// </summary>
        Custom
    }
}
