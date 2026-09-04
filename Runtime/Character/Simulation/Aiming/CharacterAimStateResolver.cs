namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把当前 Tick 的持续瞄准输入写入可回滚模拟状态。
    /// 不负责动画、IK、摄像机或开火。
    /// </summary>
    public static class CharacterAimStateResolver
    {
        #region Public API

        public static void Update(
            ref CharacterSimulationState state,
            in CharacterInputCommand command,
            in CharacterSimulationConfig config)
        {
            /*
             * Aim 是持续输入。
             * 松开后下一个有效 Tick 会自然变为 false，
             * 不依赖一次性的“退出瞄准”消息。
             */
            state.IsAiming = command.HasButton(CharacterInputButtons.Aim);

            /*
             * 无论是否正在瞄准都保存 Pitch，
             * 这样进入瞄准时不会先跳回 0 度。
             */
            state.ViewPitch = config.ClampViewPitch(command.ViewPitch);
        }

        #endregion
    }
}
