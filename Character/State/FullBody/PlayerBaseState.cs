using System.Collections;
using System.Collections.Generic;
using Animancer;
using NiumaTPC.Item.Config;
using NiumaTPC.Item.Core.Animation;
using NiumaTPC.Item.Core.Animation.Base;
using NiumaTPC.Item.RuntimeData;
using NiumaTPC.Core.StateMachine;
using UnityEngine;

namespace NiumaTPC.Item.State.FullBody
{
    /// <summary>
    /// 玩家基础状态抽象类
    /// 封锁所有玩家状态的通用引用与工具方法
    /// 统一执行顺序 先强制转移再状态自身逻辑
    /// </summary>
    public abstract class PlayerBaseState : StateBase
    {
        protected NiumaCharacterController player;
        protected PlayerRuntimeData data;
        protected PlayerSO config;
        protected IAnimationFacade AnimationFacade;

        protected PlayerBaseState(NiumaCharacterController player)
        {
            this.player = player;
            this.data = player.RuntimeData;
            this.config = player.Config;
            this.AnimationFacade = player.AnimationFacade;
        }

        // 统一的LogicUpdate流程 
        // (1) 先检查全局强制转移 高优先级拦截器
        // (2) 再执行状态自身的逻辑 确保拦截器有最高优先级
        public sealed override void LogicUpdate()
        {
            if (CheckInterrupts()) return;
            UpdateStateLogic();
        }
        
        // 全局强制转移检测 
        // 通过拦截器集合来解耦状态之间的硬依赖 
        protected virtual bool CheckInterrupts()
        {
            return player.InterruptProcessor.TryProcessInterrupts(this);
        }

        // 状态自身的正常逻辑 
        // 各个派生状态在这里实现自己的核心行为 
        protected abstract void UpdateStateLogic();

         // 选择动画播放选项并播放 
        // 优先使用 NextStatePlayOptions 临时覆写 否则使用默认选项
        // 这样设计允许其他系统临时改变下一个状态的播放参数 
        protected void ChooseOptionsAndPlay(ClipTransition clip)
        {
            if (AnimationFacade == null)
            {
                Debug.LogError($"[{nameof(PlayerBaseState)}] AnimFacade 没有初始化!");
                return;
            }

            // 优先级 NextStatePlayOptions 默认值
            var options = data.NextStatePlayOptions ?? AnimPlayOptions.Default;
            options.Layer = 0;
            
            AnimationFacade.PlayTransition(clip, options);
            data.NextStatePlayOptions = null;
        }
    }
}
