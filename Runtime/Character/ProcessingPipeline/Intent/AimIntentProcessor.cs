using NiumaTPC.Character.RuntimeData;

namespace NiumaTPC.Character.ProcessingPipeline.Intent
{
    /// <summary>
    /// 处理帧级开火意图
    /// 瞄准输入由 Tick 命令读取，瞄准状态由模拟结果写回
    /// </summary>
    public class AimIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public AimIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        // 同时支持按下开火和持续按住，不直接修改瞄准状态
        public void Update(in ProcessedInputData input)
        {
            if (input.LeftMouseHeld || input.LeftMousePressed)
            {
                _data.WantsToFire = true;
            }
        }
    }
}