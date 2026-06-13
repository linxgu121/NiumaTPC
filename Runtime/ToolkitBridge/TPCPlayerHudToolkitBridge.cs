using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaTPC.ToolkitBridge
{
    public sealed class TPCPlayerHudToolkitBridge : MonoBehaviour
    {
        [Tooltip("拖核心场景或当前场景中的 UIToolkitUIManager。为空时会自动查找。")]
        [SerializeField] private UIToolkitUIManager uiManager;
        [Tooltip("玩家 HUD ViewId，需要在 UIToolkitViewRegistrySO 中注册。")]
        [SerializeField] private string playerHudViewId = "PlayerHud";
        [SerializeField] private bool openOnEnable = true;
        [SerializeField] private bool autoOpenView = true;
        [SerializeField] private bool logWarnings = true;

        private TPCHudViewData _panel = new TPCHudViewData();
        private TPCHudViewData _lastPanel;
        private int _revision;

        private void OnEnable()
        {
            if (openOnEnable)
                RefreshHud();
        }

        private void OnDisable()
        {
            if (uiManager != null)
                uiManager.CloseView(playerHudViewId);
        }

        public void SetCrosshairVisible(bool visible)
        {
            _panel.CrosshairVisible = visible;
            BumpAndRefresh();
        }

        public void SetCrosshairHighlighted(bool highlighted)
        {
            _panel.CrosshairHighlighted = highlighted;
            BumpAndRefresh();
        }

        public void SetControlPrompt(string text)
        {
            _panel.ControlPromptText = text;
            BumpAndRefresh();
        }

        public void ClearControlPrompt()
        {
            _panel.ControlPromptText = null;
            BumpAndRefresh();
        }

        public void SetControlHints(string[] hints)
        {
            _panel.ControlHints = hints ?? System.Array.Empty<string>();
            BumpAndRefresh();
        }

        public void HideHud()
        {
            if (EnsureUIManager())
                uiManager.CloseView(playerHudViewId);
        }

        public void RefreshHud()
        {
            if (!EnsureUIManager())
                return;

            _panel.Revision = _revision;
            var update = new TPCHudUIUpdate(TPCHudUIUpdateType.Refresh, _revision, _panel, _lastPanel);
            if (!uiManager.RefreshView(playerHudViewId, update) && autoOpenView)
                uiManager.OpenView(playerHudViewId, update);
            _lastPanel = Clone(_panel);
        }

        private void BumpAndRefresh()
        {
            _revision = _revision == int.MaxValue ? int.MaxValue : _revision + 1;
            RefreshHud();
        }

        private bool EnsureUIManager()
        {
            if (uiManager != null)
                return true;
#if UNITY_2023_1_OR_NEWER
            uiManager = FindFirstObjectByType<UIToolkitUIManager>();
#else
            uiManager = FindObjectOfType<UIToolkitUIManager>();
#endif
            if (uiManager != null)
                return true;
            if (logWarnings)
                Debug.LogWarning("[TPCPlayerHudToolkitBridge] 未找到 UIToolkitUIManager，请拖核心场景 UIRoot 上的管理器。", this);
            return false;
        }

        private static TPCHudViewData Clone(TPCHudViewData source)
        {
            if (source == null)
                return null;
            return new TPCHudViewData
            {
                Revision = source.Revision,
                CrosshairVisible = source.CrosshairVisible,
                CrosshairHighlighted = source.CrosshairHighlighted,
                ControlPromptText = source.ControlPromptText,
                ControlHints = source.ControlHints != null ? (string[])source.ControlHints.Clone() : System.Array.Empty<string>()
            };
        }
    }
}
