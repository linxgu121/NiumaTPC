#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;

namespace NiumaTPC
{
    /// 【编辑器自动工具】第三方插件依赖检测与宏管理
    ///
    /// 功能：
    /// 1. 自动检测项目中是否导入了第三方插件（Cinemachine/FinalIK/Unity动画装备）
    /// 2. 自动添加/移除对应的编译宏（Scripting Define Symbols）
    /// 3. 让框架的可选功能模块，在缺少插件时不会编译报错
    ///
    /// 工作方式：通过检测【关键类是否存在】判断插件是否安装，不受导入方式影响（UPM/Assets/Plugins/Dll都支持）
    [InitializeOnLoad]
    internal static class NiumaTPCDependencyDefines
    {
        private const string DefineUar = "NiumaTPC_HAS_UAR";
        private const string DefineFinalIk = "NiumaTPC_HAS_FINALIK";
        private const string DefineCinemachine = "NiumaTPC_HAS_CINEMACHINE";

        static NiumaTPCDependencyDefines()
        {
            UpdateDefines();
         
            // 定义三个框架支持的第三方插件编译宏
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ => UpdateDefines();
        }

        private static void UpdateDefines()
        {
            // 检测方式：通过【查找关键类】判断插件是否存在，支持任何导入方式
            bool hasUar = HasType("UnityEngine.Animations.Rigging.RigBuilder", "UnityEngine.Animations.Rigging");

            // 检测 FinalIK 插件
            // 说明：FinalIK通常以源码形式导入，存在于公共程序集，因此需要检查两个可能的程序集
            bool hasFinalIk = HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp-firstpass") || HasType("RootMotion.FinalIK.AimIK", "Assembly-CSharp");

            bool hasCinemachine = HasType("Cinemachine.CinemachineBrain", "Cinemachine");

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            SetDefine(group, DefineUar, hasUar);
            SetDefine(group, DefineFinalIk, hasFinalIk);
            SetDefine(group, DefineCinemachine, hasCinemachine);
        }

        private static bool HasType(string fullTypeName, string preferredAssemblyName)
        {
            // 快速路径：优先从指定程序集查找（速度更快
            if (Type.GetType($"{fullTypeName}, {preferredAssemblyName}") != null)
                return true;

            //降级方案：遍历所有已加载的程序集，查找目标类型
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullTypeName, false) != null)
                        return true;
                }
                catch { }
            }

            return false;
        }

        private static void SetDefine(BuildTargetGroup group, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            bool has = defines.Contains(define);
            if (enabled && !has)
            {
                defines.Add(define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
            else if (!enabled && has)
            {
                defines.RemoveAll(d => d == define);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
            }
        }
    }
}
#endif