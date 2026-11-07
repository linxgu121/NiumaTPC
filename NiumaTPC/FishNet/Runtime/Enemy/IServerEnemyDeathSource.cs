using System;
using NiumaTPC.Core.Combat;
using UnityEngine;

namespace NiumaTPC.FishNet.Enemy
{
    /// <summary>
    /// 向服务器战局系统暴露统一的敌人死亡事件
    /// 不规定敌人使用玩家角色控制器还是轻量怪物核心
    /// </summary>
    public interface IServerEnemyDeathSource
    {
        /// <summary>
        /// 提供死亡来源所在的Unity组件
        /// 用于网络对象查找、日志定位和Unity对象失效检查
        /// </summary>
        Component SourceComponent { get; }

        /// <summary>
        /// 服务器首次确认当前敌人死亡时触发
        /// </summary>
        event Action<CharacterDeathInfo> OnServerDied;
    }
}
