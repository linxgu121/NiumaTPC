using NiumaTPC.Character.RuntimeData;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把固定 Tick 动作状态转换成旧状态机能够消费的一次性表现意图
    /// 这里只触发动画，不产生角色位移
    /// </summary>
    public static class CharacterActionPresentationBridge
    {
        #region Public API

        public static void Apply(
            PlayerRuntimeData data,
            CharacterActionType nextActionType,
            uint nextActionTick,
            CharacterActionDirection nextActionDirection)
        {
            if(data == null)
            {
                return;
            }

             /*
             * 只有 None -> Roll/Dodge，或动作类型发生变化时才触发动画。
             * 同一动作后续 Tick、服务器校正及预测重演不会每 Tick 重播动画。
             */
            bool actionStarted =
                nextActionType != CharacterActionType.None &&
                data.SimulationActionType != nextActionType;

            // 持续状态每个 Tick 都要同步，不能只在动作开始时写入。
            data.SimulationActionType = nextActionType;
            data.SimulationActionTick = nextActionTick;
            data.SimulationActionDirection = nextActionDirection;

             if (!actionStarted)
            {
                return;
            }

            // 旧 Roll/Dodge/Slide 状态根据这个局部角度选择八方向动画。
            data.DesiredLocalMoveAngle = ToLocalAngle(nextActionDirection);

            switch (nextActionType)
            {
                case CharacterActionType.Roll:
                    data.WantsToRoll = true;
                    break;

                case CharacterActionType.Dodge:
                    data.WantsToDodge = true;
                    break;

                case CharacterActionType.Slide:
                    data.WantsToSlide = true;
                    break;
            }
        }

        #endregion

        #region Direction Conversion(方向转换)

        /// <summary>
        /// 将8方向转化为角色局部空间下的角度
        /// </summary>
        private static float ToLocalAngle(CharacterActionDirection direction)
        {
            return direction switch
            {
                CharacterActionDirection.Forward => 0f,
                CharacterActionDirection.ForwardRight => 45f,
                CharacterActionDirection.Right => 90f,
                CharacterActionDirection.BackRight => 135f,
                CharacterActionDirection.Back => 180f,
                CharacterActionDirection.BackLeft => -135f,
                CharacterActionDirection.Left => -90f,
                CharacterActionDirection.ForwardLeft => -45f,
                _ => 0f
            };
        }

        #endregion
    }
}