using System;
using NiumaTPC.Character.Event;
using NiumaTPC.Character.State.Core;

namespace NiumaTPC.Character.State.Override
{
    /// <summary>
    /// 处理角色死亡后的状态锁定与表现
    /// 网络回收和玩家复活由服务器生命周期组件负责
    /// </summary>
    [Serializable]
    public sealed class PlayerDeathState : PlayerBaseState
    {
        public PlayerDeathState(NiumaCharacterController player) : base(player) { }

        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            data.SfxQueue.Enqueue(PlayerSfxEvent.Death);

            data.IsDead = true;
            data.Arbitration.IsDead = true;
            data.Arbitration.BlockInput = true;
            data.Arbitration.BlockUpperBody = true;
            data.Arbitration.BlockFacial = true;
            data.Arbitration.BlockIK = true;
            data.Arbitration.BlockInventory = true;

            var clip = config.Core.DeathAnim;
            if (clip != null)
                AnimationFacade.PlayFullBodyAction(clip, 0.05f);

        }

        // 死亡状态保持到服务器执行复活或网络回收
        protected override void UpdateStateLogic()
        {
        }

        // 死亡表现状态不通过旧驱动追加重力位移
        public override void PhysicsUpdate()
        {
        }

        public override void Exit()
        {
            AnimationFacade.StopFullBodyAction();
        }

    }
}
