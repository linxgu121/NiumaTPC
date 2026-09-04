namespace NiumaTPC.Character.State.Enum
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
        Death,
        //为了不弄坏枚举序号，虽然不美观但都得加在最后面
        Slide
    }
}