using UnityEditor;
using UnityEngine;

namespace NiumaHotUpdate.Editor
{
    /// <summary>
    /// 提供 Windows 热更版本输入、路径预览和发布前置校验入口
    /// 当前阶段支持校验、编译 DLL、替换 DLL Bytes 和生成清单，Addressables增量构建
    /// </summary>
    internal sealed class HotUpdatePublisherWindow : EditorWindow
    {
        #region Constants(常量)

        private const string MenuPath =
            "Tools/NiumaHotUpdate/Hot Update Publisher";

        private const string HotUpdateVersionPreferenceKey =
            "NiumaHotUpdate.Editor.HotUpdateVersion";

        #endregion

        #region Window State(窗口状态)

        private string _hotUpdateVersion;
        private string _resultMessage;
        private MessageType _resultMessageType;
        private bool _showPaths;
        private bool _showStepActions = true;
        private Vector2 _scrollPosition;

        #endregion

        #region Window Lifecycle(窗口生命周期)

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            HotUpdatePublisherWindow window =
                GetWindow<HotUpdatePublisherWindow>("Niuma Hot Update");

            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _hotUpdateVersion = EditorPrefs.GetString(
                HotUpdateVersionPreferenceKey,
                "0.1.1");

            _resultMessage = "尚未执行发布前置校验";
            _resultMessageType = MessageType.Info;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(
                _scrollPosition);

            DrawHeader();
            DrawVersionFields();
            DrawEnvironment();
            DrawPaths();
            DrawValidation();

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Drawing(界面绘制)

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "HybridCLR 热更发布",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "输入热更版本后可一键完成 DLL 编译、SHA256 清单生成和 Addressables 增量构建",
                MessageType.Info);

            EditorGUILayout.Space(6f);
        }

        private void DrawVersionFields()
        {
            EditorGUILayout.LabelField(
                "版本",
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "基础客户端版本",
                    PlayerSettings.bundleVersion);
            }

            _hotUpdateVersion = EditorGUILayout.TextField(
                "热更新版本",
                _hotUpdateVersion);

            EditorGUILayout.Space(6f);
        }

        private void DrawEnvironment()
        {
            EditorGUILayout.LabelField(
                "当前环境",
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup(
                    "构建平台",
                    EditorUserBuildSettings.activeBuildTarget);

                EditorGUILayout.TextField(
                    "基础版本来源",
                    "PlayerSettings.bundleVersion");
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawPaths()
        {
            _showPaths = EditorGUILayout.Foldout(
                _showPaths,
                "发布路径预览",
                true);

            if (!_showPaths)
            {
                return;
            }

            HotUpdatePublishContext context = CreateContext();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "编译 DLL",
                    context.CompiledDllPath);

                EditorGUILayout.TextField(
                    "目标 DLL Bytes",
                    context.DllBytesFilePath);

                EditorGUILayout.TextField(
                    "热更清单",
                    context.ManifestFilePath);

                EditorGUILayout.TextField(
                    "基础状态文件",
                    context.ContentStatePath);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField(
                "发布操作",
                EditorStyles.boldLabel);

            if (GUILayout.Button("构建并归档基础版本资源", GUILayout.Height(36f)))
            {
                BuildAndArchiveBaseline();
            }

            EditorGUILayout.HelpBox(
                "仅在创建新的完整 Player 基线时使用，本操作会完整构建 Addressables 并覆盖当前版本的归档状态文件",
                MessageType.Warning);

            EditorGUILayout.Space(6f);

            if (GUILayout.Button(
                    "一键发布热更新",
                    GUILayout.Height(40f)))
            {
                PublishAll();
            }

            EditorGUILayout.Space(6f);

            _showStepActions = EditorGUILayout.Foldout(
                _showStepActions,
                "分步操作",
                true);

            if (_showStepActions)
            {
                if (GUILayout.Button(
                        "执行发布前置校验",
                        GUILayout.Height(28f)))
                {
                    ValidatePublish();
                }

                if (GUILayout.Button(
                        "编译并替换 DLL Bytes",
                        GUILayout.Height(28f)))
                {
                    CompileAndReplaceDll();
                }

                if (GUILayout.Button(
                        "生成热更清单",
                        GUILayout.Height(28f)))
                {
                    WriteManifest();
                }

                if (GUILayout.Button(
                        "构建 Addressables 增量内容",
                        GUILayout.Height(28f)))
                {
                    BuildAddressablesContent();
                }
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                _resultMessage,
                _resultMessageType);
        }

        #endregion

        #region Validation(发布校验)

        /// <summary>
        /// 根据窗口版本和当前 Unity 环境执行无写入的发布前置校验
        /// </summary>
        private void ValidatePublish()
        {
            HotUpdatePublishContext context = CreateContext();

            HotUpdatePublishValidationResult result =
                HotUpdatePublishValidator.Validate(context);

            EditorPrefs.SetString(
                HotUpdateVersionPreferenceKey,
                _hotUpdateVersion);

            _resultMessage =
                $"[{result.ErrorCode}] {result.Message}";

            _resultMessageType = result.Succeeded
                ? MessageType.Info
                : MessageType.Error;

            if (result.Succeeded)
            {
                Debug.Log(
                    $"[NiumaHotUpdate发布] 校验通过，Base={context.BasePlayerVersion}，HotUpdate={context.HotUpdateVersion}");
            }
            else
            {
                Debug.LogError(
                    $"[NiumaHotUpdate发布] 校验失败，ErrorCode={result.ErrorCode}，Message={result.Message}");
            }
        }

        /// <summary>
        /// 用户确认后编译 Release 热更程序集并替换 Addressable DLL Bytes
        /// </summary>
        private void CompileAndReplaceDll()
        {
            HotUpdatePublishContext context = CreateContext();

            if (!EditorUtility.DisplayDialog(
                    "确认编译热更 DLL",
                    $"基础客户端：{context.BasePlayerVersion}\n热更版本：{context.HotUpdateVersion}\n\n本操作将重新编译并替换现有 DLL Bytes",
                    "继续",
                    "取消"))
            {
                return;
            }

            EditorPrefs.SetString(
                HotUpdateVersionPreferenceKey,
                _hotUpdateVersion);

            HotUpdatePublishOperationResult result;

            try
            {
                EditorUtility.DisplayProgressBar(
                    "NiumaHotUpdate",
                    "正在编译并替换热更 DLL",
                    0.5f);

                result =
                    HotUpdateAssemblyPublisher.CompileAndReplace(context);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _resultMessage = $"[{result.ErrorCode}] {result.Message}";

            _resultMessageType = result.Succeeded
                ? MessageType.Info
                : MessageType.Error;

            if (result.Succeeded)
            {
                Debug.Log($"[NiumaHotUpdate发布] {result.Message}");
            }
            else
            {
                Debug.LogError($"[NiumaHotUpdate发布] 编译或替换失败，ErrorCode={result.ErrorCode}，Message={result.Message}");
            }
        }

        /// <summary>
        /// 用户确认后按照窗口版本生成并替换热更发布清单
        /// </summary>
        private void WriteManifest()
        {
            HotUpdatePublishContext context = CreateContext();

            if (!EditorUtility.DisplayDialog(
                    "确认生成热更清单",
                    $"基础客户端：{context.BasePlayerVersion}\n热更版本：{context.HotUpdateVersion}\n\n本操作将替换现有 HotUpdateManifest.json",
                    "继续",
                    "取消"))
            {
                return;
            }

            EditorPrefs.SetString(
                HotUpdateVersionPreferenceKey,
                _hotUpdateVersion);

            HotUpdatePublishOperationResult result =
                HotUpdateManifestPublisher.Write(context);

            _resultMessage =
                $"[{result.ErrorCode}] {result.Message}";

            _resultMessageType = result.Succeeded
                ? MessageType.Info
                : MessageType.Error;

            if (result.Succeeded)
            {
                Debug.Log(
                    $"[NiumaHotUpdate发布] {result.Message}");
            }
            else
            {
                Debug.LogError(
                    $"[NiumaHotUpdate发布] 清单生成失败，ErrorCode={result.ErrorCode}，Message={result.Message}");
            }
        }

        /// <summary>
        /// 用户确认后完整构建 Addressables 并自动归档当前基础版本状态
        /// </summary>
        private void BuildAndArchiveBaseline()
        {
            HotUpdatePublishContext context = CreateContext();

            if (!EditorUtility.DisplayDialog(
                    "确认构建基础版本资源",
                    $"基础客户端：{context.BasePlayerVersion}\n热更新版本：{context.HotUpdateVersion}\n归档位置：{context.ContentStatePath}\n\n本操作将执行完整构建并覆盖同版本状态文件",
                    "开始构建",
                    "取消"))
            {
                return;
            }

            HotUpdatePublishOperationResult result;

            try
            {
                EditorUtility.DisplayProgressBar(
                    "NiumaHotUpdate",
                    "正在完整构建并归档基础版本资源",
                    0.5f);

                result = HotUpdateAddressablesPublisher.BuildAndArchiveBaseline(context);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ApplyOperationResult(
                result,
                "基础版本资源构建");
        }

        /// <summary>
        /// 用户确认后基于基础客户端状态文件构建 Addressables 增量内容
        /// </summary>
        private void BuildAddressablesContent()
        {
            HotUpdatePublishContext context = CreateContext();

            if (!EditorUtility.DisplayDialog(
                    "确认构建 Addressables 增量内容",
                    $"基础客户端：{context.BasePlayerVersion}\n热更版本：{context.HotUpdateVersion}\n状态文件：{context.ContentStatePath}",
                    "继续",
                    "取消"))
            {
                return;
            }

            HotUpdatePublishOperationResult result;

            try
            {
                EditorUtility.DisplayProgressBar(
                    "NiumaHotUpdate",
                    "正在构建 Addressables 增量内容",
                    0.75f);

                result =
                    HotUpdateAddressablesPublisher.Build(context);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ApplyOperationResult(
                result,
                "Addressables 增量构建");
        }

        /// <summary>
        /// 用户确认后按固定顺序执行完整热更发布流水线
        /// </summary>
        private void PublishAll()
        {
            HotUpdatePublishContext context = CreateContext();

            if (!EditorUtility.DisplayDialog(
                    "确认发布热更新",
                    $"基础客户端：{context.BasePlayerVersion}\n热更版本：{context.HotUpdateVersion}\n\n将依次编译 DLL、生成 SHA256 清单并构建 Addressables 增量内容",
                    "开始发布",
                    "取消"))
            {
                return;
            }

            EditorPrefs.SetString(
                HotUpdateVersionPreferenceKey,
                _hotUpdateVersion);

            HotUpdatePublishOperationResult result;

            try
            {
                result = HotUpdatePublishPipeline.Publish(
                    context,
                    (message, progress) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "NiumaHotUpdate",
                            message,
                            progress);
                    });
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ApplyOperationResult(
                result,
                "完整热更发布");
        }

        private void ApplyOperationResult(
            HotUpdatePublishOperationResult result,
            string operationName)
        {
            _resultMessage =
                $"[{result.ErrorCode}] {result.Message}";

            _resultMessageType = result.Succeeded
                ? MessageType.Info
                : MessageType.Error;

            if (result.Succeeded)
            {
                Debug.Log(
                    $"[NiumaHotUpdate发布] {operationName}成功，{result.Message}");
            }
            else
            {
                Debug.LogError(
                    $"[NiumaHotUpdate发布] {operationName}失败，ErrorCode={result.ErrorCode}，Message={result.Message}");
            }
        }

        #endregion

        #region Context(发布上下文)

        private HotUpdatePublishContext CreateContext()
        {
            return new HotUpdatePublishContext(
                PlayerSettings.bundleVersion,
                _hotUpdateVersion,
                EditorUserBuildSettings.activeBuildTarget);
        }

        #endregion
    }
}
