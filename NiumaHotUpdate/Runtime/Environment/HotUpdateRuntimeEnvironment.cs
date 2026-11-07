using UnityEngine;

namespace NiumaHotUpdate
{
    /// <summary>
    /// 将 Unity 运行环境转换为热更发布协议使用的稳定标识
    /// </summary>
    public static class HotUpdateRuntimeEnvironment
    {
        #region Constants(常量)

        public const string StandaloneWindows64 = "StandaloneWindows64";

        #endregion

        #region Properties(环境属性)

        /// <summary>
        /// 当前完整客户端的基础版本
        /// 对应 Player Settings 中的 Version
        /// </summary>
        public static string CurrentBasePlayerVersion => Application.version;

        #endregion

        #region Public API(公共接口)

        /// <summary>
        /// 将当前 Unity 运行平台转换为热更清单使用的平台名称
        /// Windows Editor 映射到 Windows 64 位仅用于编辑器验证
        /// </summary>
        public static bool TryGetCurrentPlatformName(out string platformName)
        {
            return TryGetPlatformName(Application.platform, out platformName);
        }

        /// <summary>
        /// 将指定 RuntimePlatform 转换为热更清单使用的平台名称
        /// 当前阶段只允许 Windows 64 位客户端
        /// </summary>
        public static bool TryGetPlatformName(RuntimePlatform runtimePlatform, out string platformName)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    platformName = StandaloneWindows64;
                    return true;

                default:
                    platformName = string.Empty;
                    return false;
            }
        }

        #endregion
    }
}
