using System.IO;
using HybridCLR;
using UnityEditor;
using UnityEngine;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 保存一次 Windows 热更发布所需的版本、输入文件和目标文件路径
    /// 所有路径均从项目目录推导，避免手工选择错误文件
    /// </summary>
    internal sealed class HotUpdatePublishContext
    {
        #region Constants(常量)

        public const string HotUpdateAssemblyName = "NiumaHotUpdate.Hotfix";
        public const string HotUpdateAssemblyAddress = "hotupdate/windows/assemblies/niuma-hotupdate-hotfix";
        public const string HotUpdateEntryTypeName = "NiumaHotUpdate.Hotfix.HotUpdateEntry";
        public const string ContentRootAssetPath = "Assets/Script/NiumaHotUpdate/Content/Windows";
        public const string ManifestAssetPath = ContentRootAssetPath + "/HotUpdateManifest.json";
        public const string DllBytesAssetPath = ContentRootAssetPath + "/NiumaHotUpdate.Hotfix.dll.bytes";

        #endregion

        #region Publish Data(发布数据)

        public string BasePlayerVersion { get; }
        public string HotUpdateVersion { get; }
        public BuildTarget Target { get; }

        public string ProjectRoot { get; }
        public string CompiledDllPath { get; }
        public string DllBytesFilePath { get; }
        public string ManifestFilePath { get; }
        public string ContentStatePath { get; }

        #endregion

        #region Construction(构造)

        public HotUpdatePublishContext(string basePlayerVersion, string hotUpdateVersion, BuildTarget target)
        {
            BasePlayerVersion = basePlayerVersion?.Trim() ?? string.Empty;
            HotUpdateVersion = hotUpdateVersion?.Trim() ?? string.Empty;
            Target = target;

            ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            CompiledDllPath = Path.Combine(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), HotUpdateAssemblyName + ".dll");
            DllBytesFilePath = ConvertAssetPathToFilePath(DllBytesAssetPath);
            ManifestFilePath = ConvertAssetPathToFilePath(ManifestAssetPath);
            ContentStatePath = Path.Combine(
                ProjectRoot,
                "AddressablesContentState",
                "Windows",
                BasePlayerVersion,
                "addressables_content_state.bin");
        }

        #endregion

        #region Path Helpers(路径辅助)

        private string ConvertAssetPathToFilePath(string assetPath)
        {
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ProjectRoot, relativePath));
        }

        #endregion
    }
}
