using System.Collections;
using System.Collections.Generic;
using Animancer;
using NiumaTPC.Character.Core.Animation;
using NiumaTPC.Character.Expression;
using NiumaTPC.Character.RuntimeData;
using NiumaTPC.Core.StateMachine;
using UnityEditor;
using UnityEngine;

namespace NiumaTPC.Character.State.UpperBody
{
    /// <summary>
    /// 上半身基础状态抽象类 
    /// 与PlayerBaseState地位相同 但管理上半身子状态机的独立流程 
    /// UpperBodyController 在 PlayerController 的 Start 里才初始化 所以这里采用延迟加载
    /// </summary>
    public abstract class UpperBodyBaseState : StateBase
    {
        protected NiumaCharacterController player;
        protected PlayerRuntimeData data;
        protected UpperBodyController controller;

        public UpperBodyBaseState(NiumaCharacterController player)
        {
            this.player  = player;
            this.data = player.RuntimeData;
        }
        
        /// <summary>
        /// 统一的LogicUpdate流程 延迟加载controller避免启动顺序问题 
        /// </summary>
        public sealed override void LogicUpdate()
        {
            if (controller == null) controller = player.UpperBodyController;

            if (CheckInterrupts()) return;
        }

        public override void PhysicsUpdate() { }

        /// <summary>
        /// 上半身的拦截器检测 负责检查是否能进入特定的上半身状态 
        /// </summary>
        protected virtual bool CheckInterrupts()
        {
            if (controller == null || controller.InterruptProcessor == null) return false;
            return controller.InterruptProcessor.TryProcessInterrupts(this);
        }

        /// <summary>
        /// 状态自身的正常逻辑 
        /// </summary>
        protected abstract void UpdateStateLogic();

        /// <summary>
        /// 播放上半身动画的通用方法
        /// 默认 Layer = 1 上半身层 使用 NextStatePlayOptions 或默认选项
        /// 这里的层级设置确保上半身动画只影响特定骨骼 与下半身互不干扰
        /// </summary>
        protected void ChooseOptionsAndPlay(ClipTransition clip)
        {
            if (player.AnimationFacade == null)
            {
                Debug.LogError($"[{nameof(UpperBodyBaseState)}] AnimFacade没有初始化!");
                return;
            }

            //优先级 NextStatePlayOptions 默认值
            var options = data.NextStatePlayOptions ?? AnimPlayOptions.Default;
            options.Layer = 1;

            player.AnimationFacade.PlayTransition(clip, options);
            data.NextStatePlayOptions = null;
        }

    }
    
}
