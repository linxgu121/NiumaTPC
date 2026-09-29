using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 单个参与者的选择状态。
    /// 描述选择结果，不代表当前是否允许操作。
    /// </summary>
    public enum PlayerCharacterSelectionState : byte
    {
        /// <summary>
        /// 尚无候选角色。
        /// </summary>
        Unselected = 0,

        /// <summary>
        /// 已选择候选，但尚未确认，不占用角色。
        /// </summary>
        Selected = 1,

        /// <summary>
        /// 已确认
        /// </summary>
        Confirmed = 2
    }
}
