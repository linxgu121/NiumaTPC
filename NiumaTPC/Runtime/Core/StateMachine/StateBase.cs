using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Core.StateMachine
{
    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class StateBase
    {
        /// <summary>
        /// 进入状态
        /// </summary>
        public abstract void Enter();
        /// <summary>
        /// 逻辑更新（每帧）
        /// </summary>
        public abstract void LogicUpdate();
        /// <summary>
        /// 物理更新
        /// </summary>
        public abstract void PhysicsUpdate();
        //退出状态
        public abstract void Exit();
    }
}
