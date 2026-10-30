namespace NiumaTPC.Character.State.UpperBody
{
    /// <summary>
    /// 上半身不可以用状态
    /// 当翻越或翻滚等动作暂时占用整个身体时 强制解除当前装备实体
    /// </summary>
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(NiumaCharacterController player) : base(player) { }

        public override void Enter()
        {
            player.AnimationFacade.SetLayerWeight(1,0f,0.2f);
            
            // 强制卸下当前装备 确保玩家回复时能正确保存装备选择
            // 注意 不要重置 RuntimeData.CurrentItem
            // RuntimeData.CurrentItem 才是真正的装备装备图 装备选择
            // Unavailable 只清理当前装备实体 不修改玩家选择的装备槽位
            if (player != null && player.EquipmentDriver != null)
            {
                player.EquipmentDriver.UnequipCurrentItem();
            }
        }

        // 退出状态 上半身权限由下一个状态 HoldItem 重新获得
        public override void Exit()
        {
            
        }

        // 状态逻辑 为了安全 此退出逻辑是唯一不能中断的条件
        protected override void UpdateStateLogic()
        {
            
        }
    }
}
