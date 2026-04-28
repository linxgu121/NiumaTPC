using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.State.Core;
using UnityEngine;

namespace NiumaTPC.Character.State.Base
{
    /// <summary>
    /// 状态拦截器（全局拦截器的父类）
    /// </summary>
    public abstract class StateInterceptorSO : ScriptableObject
    {
        /// <summary>
        /// 尝试拦截当前状态
        /// </summary>
        /// <param name="player">控制器</param>
        /// <param name="currentState">当前运行的状态</param>
        /// <param name="nextState">拦截成功，切换到新状态</param>
        /// <returns></returns>
        public abstract bool TryIntercept(NiumaCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState);
    }
}
