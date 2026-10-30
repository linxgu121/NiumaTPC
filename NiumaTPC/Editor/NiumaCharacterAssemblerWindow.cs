using System;
using System.Collections.Generic;
using System.Linq;
using NiumaTPC.Character;
using NiumaTPC.Character.Combat;
using NiumaTPC.Character.Input.AI;
using NiumaTPC.Character.Input.Player;
using NiumaTPC.Core.Faction;
using NiumaTPC.Enemy.Brain;
using NiumaTPC.Enemy.Combat;
using NiumaTPC.Item;
using NiumaTPC.Role;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NiumaTPC.Editor
{
    /// <summary>
    /// 为所选角色根节点补齐玩家或敌人的必需组件
    /// 只添加缺失组件，不修改引用、配置或子物体
    /// </summary>
    public sealed class NiumaCharacterAssemblerWindow : EditorWindow
    {
        private enum AssemblyKind
        {
            Player,
            Enemy
        }

        #region Constants(常量)

        private const string MenuPath = "Tools/NiumaTPC/角色与敌人组件装配";

        private static readonly string[] PlayerNetworkComponentNames =
        {
            "FishNet.Object.NetworkObject",
            "NiumaTPC.FishNet.NiumaFishNetPredictionDriver",
            "NiumaTPC.FishNet.NiumaFishNetCameraBinder",
            "NiumaTPC.FishNet.Items.WeaponDriver",
            "NiumaTPC.FishNet.Items.WeaponMeleeAttackSource"
        };

        private static readonly string[] EnemyNetworkComponentNames =
        {
            "FishNet.Object.NetworkObject",
            "NiumaTPC.FishNet.NiumaFishNetPredictionDriver",
            "NiumaTPC.FishNet.Enemy.ServerEnemyControlBridge"
        };

        #endregion

        #region Window State(窗口状态)

        [SerializeField] private GameObject target;
        [SerializeField] private AssemblyKind assemblyKind;

        private Vector2 scrollPosition;
        private string lastResult = "尚未执行";
        private bool useUndo;

        #endregion

        #region Unity Lifecycle(Unity 生命周期)

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<NiumaCharacterAssemblerWindow>("TPC组件装配");
        }

        private void OnEnable()
        {
            if (target == null) target = Selection.activeGameObject;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("角色与敌人组件装配", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("本工具只补齐根节点缺失组件，不填写引用、不删除已有组件、不创建子物体。MeleeSensor、PerceptionOrigin、GraphicsRoot和DamageHurtbox请按敌人模块设计文档手动创建", MessageType.Info);

            target = (GameObject)EditorGUILayout.ObjectField("角色根节点", target, typeof(GameObject), true);
            if (GUILayout.Button("使用当前选择")) target = Selection.activeGameObject;

            assemblyKind = (AssemblyKind)EditorGUILayout.EnumPopup("装配类型", assemblyKind);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button(assemblyKind == AssemblyKind.Player ? "补齐玩家组件" : "补齐敌人组件", GUILayout.Height(34f))) AssembleSelected();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastResult, lastResult.Contains("失败") || lastResult.Contains("未找到") ? MessageType.Error : MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Assembly(组件装配)

        private void AssembleSelected()
        {
            if (!CanAssemble()) return;
            RunOnTarget(AssembleRoot);
        }

        /// <summary>
        /// 检查是否选择了可编辑的角色根节点
        /// 不检查引用和子物体配置
        /// </summary>
        private bool CanAssemble()
        {
            if (target != null) return true;

            EditorUtility.DisplayDialog("无法装配", "请先选择角色根节点或Prefab资产", "确定");
            return false;
        }

        private void RunOnTarget(Action<GameObject> action)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            bool isPrefabAsset = !string.IsNullOrEmpty(assetPath) && PrefabUtility.IsPartOfPrefabAsset(target);

            if (!isPrefabAsset)
            {
                useUndo = true;
                action(target);
                if (target.scene.IsValid()) EditorSceneManager.MarkSceneDirty(target.scene);
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                useUndo = false;
                action(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
        }

        private void AssembleRoot(GameObject root)
        {
            var added = new List<string>();
            var existing = new List<string>();
            var missingTypes = new List<string>();

            if (assemblyKind == AssemblyKind.Player) AssemblePlayer(root, added, existing, missingTypes);
            else AssembleEnemy(root, added, existing, missingTypes);

            EditorUtility.SetDirty(root);
            lastResult = BuildResult(added, existing, missingTypes);
            Debug.Log($"[TPC组件装配] {root.name}\n{lastResult}", root);
            Repaint();
        }

        private void AssemblePlayer(GameObject root, ICollection<string> added, ICollection<string> existing, ICollection<string> missingTypes)
        {
            Ensure<NiumaCharacterController>(root, added, existing);
            Ensure<PlayerInputSource>(root, added, existing);
            Ensure<FixedCharacterStatsInitializer>(root, added, existing);
            Ensure<TeamAffiliation>(root, added, existing);
            Ensure<MeleeCharacterTarget>(root, added, existing);
            Ensure<MeleeTargetingController>(root, added, existing);
            Ensure<WeaponFireRequestSource>(root, added, existing);
            EnsureByName(root, PlayerNetworkComponentNames, added, existing, missingTypes);
        }

        private void AssembleEnemy(GameObject root, ICollection<string> added, ICollection<string> existing, ICollection<string> missingTypes)
        {
            Ensure<NiumaCharacterController>(root, added, existing);
            Ensure<EnemyInputSource>(root, added, existing);
            Ensure<FixedCharacterStatsInitializer>(root, added, existing);
            Ensure<TeamAffiliation>(root, added, existing);
            Ensure<EnemyBrain>(root, added, existing);
            Ensure<EnemyMeleeAttackSource>(root, added, existing);
            Ensure<MeleeCharacterTarget>(root, added, existing);
            Ensure<MeleeTargetingController>(root, added, existing);
            EnsureByName(root, EnemyNetworkComponentNames, added, existing, missingTypes);
        }

        #endregion

        #region Component Resolution(组件解析)

        private void Ensure<T>(GameObject root, ICollection<string> added, ICollection<string> existing) where T : Component
        {
            Ensure(root, typeof(T), added, existing);
        }

        private void EnsureByName(GameObject root, IEnumerable<string> fullNames, ICollection<string> added,
            ICollection<string> existing, ICollection<string> missingTypes)
        {
            foreach (string fullName in fullNames)
            {
                Type type = ResolveComponentType(fullName);
                if (type == null)
                {
                    missingTypes.Add(fullName);
                    continue;
                }

                Ensure(root, type, added, existing);
            }
        }

        private void Ensure(GameObject root, Type type, ICollection<string> added, ICollection<string> existing)
        {
            if (root.GetComponent(type) != null)
            {
                existing.Add(type.Name);
                return;
            }

            if (useUndo) Undo.AddComponent(root, type);
            else root.AddComponent(type);
            added.Add(type.Name);
        }

        private static Type ResolveComponentType(string fullName)
        {
            return TypeCache.GetTypesDerivedFrom<Component>().FirstOrDefault(type => type.FullName == fullName);
        }

        #endregion

        #region Result Formatting(结果整理)

        private static string BuildResult(IReadOnlyCollection<string> added, IReadOnlyCollection<string> existing, IReadOnlyCollection<string> missingTypes)
        {
            var lines = new List<string>
            {
                $"新增组件：{(added.Count == 0 ? "无" : string.Join("、", added))}",
                $"已有组件：{existing.Count}个"
            };

            if (missingTypes.Count > 0) lines.Add($"未找到组件类型：{string.Join("、", missingTypes)}");
            lines.Add("引用、配置和子物体仍需按设计文档手动设置");
            return string.Join("\n", lines);
        }

        #endregion
    }
}
