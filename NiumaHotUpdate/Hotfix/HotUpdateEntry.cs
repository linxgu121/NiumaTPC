using System;
using UnityEngine;

namespace NiumaHotUpdate.Hotfix
{
    /// <summary>
    /// TPC 框架第一阶段热更入口
    /// 当前用于验证旧客户端能否执行远程更新后的 C# 代码
    /// </summary>
    public sealed class HotUpdateEntry : IHotUpdateEntry
    {
        /// <summary>
        /// 检查加载器上下文并启动当前热更业务
        /// </summary>
        public void Initialize(HotUpdateEntryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Log($"[NiumaHotUpdate] 热更入口启动成功喵!Version={context.HotUpdateVersion}，Message=纯代码热更验证");
        }
    }
}
