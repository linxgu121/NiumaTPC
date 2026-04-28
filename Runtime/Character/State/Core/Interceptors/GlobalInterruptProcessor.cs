

namespace NiumaTPC.Character.State.Core.Interceptors
{
    /// <summary>
    /// 全局拦截处理器，负责在意图管线之后执行全局优先级的状态转移
    /// 包装高优先级的拦截器集合 在状态逻辑之前检查是否需要强行切换状态
    /// </summary>
    public class GlobalInterruptProcessor
    {
        private readonly NiumaCharacterController _player;

        public GlobalInterruptProcessor(NiumaCharacterController player)
        {
            _player = player;
        }

        /// <summary>
        /// 尝试处理全局拦截
        /// 依次遍历 PlayerBrainSO 中的拦截器集合 如果有拦截器返回 true 就切换状态并结束检测
        /// </summary>
        public bool TryProcessInterrupts(PlayerBaseState currentState)
        {
            //没有设置拦截器管线 直接返回
            if(_player.Config == null || _player.Config.Brain == null || _player.Config.Brain.GlobalInterceptors == null)
              return false;
            
            // 遍历拦截器管道
            var pipeline = _player.Config.Brain.GlobalInterceptors;
            for (int i = 0; i < pipeline.Count; i++)
            {
                var interceptor = pipeline[i];
                if (interceptor != null && interceptor.TryIntercept(_player, currentState, out var nextState))
                {
                    _player.StateMachine.ChangeState(nextState);
                    return true;
                }
            }

            return false;
        }
        
    }
}