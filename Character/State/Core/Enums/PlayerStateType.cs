using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Item.State.Enum
{
    /// <summary>
    /// 玩家下半身状态类型
    /// 主要用于调试和状态识别 配合状态字典进行映射
    /// </summary>
    public enum PlayerStateType
    {
        Idle,
        MoveStartState,
        MoveLoopState,
        StopState,
        Jump,
        DoubleJump,
        Fall,
        Land,
        Dodge,
        Roll,
        Vault,
        AimIdle,
        AimMove,
        Override,
        Death
    }
}