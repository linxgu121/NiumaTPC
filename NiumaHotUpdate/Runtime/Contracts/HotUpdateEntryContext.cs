using System;
using NiumaCore.Module;

namespace NiumaHotUpdate
{
    /// <summary>
    /// AOT 加载层传给热更入口的稳定启动上下文
    /// </summary>
    public class HotUpdateEntryContext
    {
        public GameContext GameContext { get; }
        public string HotUpdateVersion { get; }

        public HotUpdateEntryContext(
            GameContext gameContext,
            string hotUpdateVersion)
        {
            GameContext = gameContext ??
                throw new ArgumentNullException(nameof(gameContext));

            if (string.IsNullOrWhiteSpace(hotUpdateVersion))
            {
                throw new ArgumentException("热更版本不能为空", nameof(hotUpdateVersion));
            }

            HotUpdateVersion = hotUpdateVersion;
        }
    }
}
