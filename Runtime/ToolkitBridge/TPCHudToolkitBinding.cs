using System;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaTPC.ToolkitBridge
{
    public sealed class TPCHudToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Header("元素 name")]
        [SerializeField] private string crosshairRootName = "CrosshairRoot";
        [SerializeField] private string crosshairCenterName = "CrosshairCenter";
        [SerializeField] private string promptRootName = "ControlPromptRoot";
        [SerializeField] private string promptLabelName = "ControlPromptText";
        [SerializeField] private string hintListName = "ControlHintList";
        [SerializeField] private string hintEmptyRootName = "ControlHintEmptyRoot";
        [SerializeField] private string highlightedClass = "is-highlighted";
        [SerializeField] private string rowClass = "niuma-tpc-hint-row";
        [SerializeField] private string selectedRowClass = "is-selected";
        [SerializeField] private string disabledRowClass = "is-disabled";

        protected override string DefaultProviderId => "PlayerHud";

        public override IToolkitViewBinding CreateBinding()
        {
            return new TPCHudToolkitBinding(
                crosshairRootName,
                crosshairCenterName,
                promptRootName,
                promptLabelName,
                hintListName,
                hintEmptyRootName,
                highlightedClass,
                rowClass,
                selectedRowClass,
                disabledRowClass);
        }
    }

    public sealed class TPCHudToolkitBinding : ToolkitViewBindingBase<TPCHudUIUpdate, TPCHudToolkitViewModel>
    {
        private readonly string _crosshairRootName;
        private readonly string _crosshairCenterName;
        private readonly string _promptRootName;
        private readonly string _promptName;
        private readonly string _hintListName;
        private readonly string _hintEmptyName;
        private readonly string _highlightedClass;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private VisualElement _crosshairRoot;
        private VisualElement _crosshairCenter;
        private VisualElement _promptRoot;
        private Label _prompt;
        private readonly ToolkitListBinding<ToolkitTextRowData> _hints = new ToolkitListBinding<ToolkitTextRowData>();

        public TPCHudToolkitBinding(
            string crosshairRootName,
            string crosshairCenterName,
            string promptRootName,
            string promptName,
            string hintListName,
            string hintEmptyName,
            string highlightedClass,
            string rowClass,
            string selectedClass,
            string disabledClass)
        {
            _crosshairRootName = crosshairRootName;
            _crosshairCenterName = crosshairCenterName;
            _promptRootName = promptRootName;
            _promptName = promptName;
            _hintListName = hintListName;
            _hintEmptyName = hintEmptyName;
            _highlightedClass = string.IsNullOrWhiteSpace(highlightedClass) ? "is-highlighted" : highlightedClass.Trim();
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-tpc-hint-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
        }

        protected override void OnInitializeTyped()
        {
            _crosshairRoot = Query<VisualElement>(_crosshairRootName);
            _crosshairCenter = Query<VisualElement>(_crosshairCenterName);
            _promptRoot = Query<VisualElement>(_promptRootName);
            _prompt = QLabel(_promptName);
            _hints.Bind(Root, _hintListName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, null), _hintEmptyName);
        }

        protected override void OnRefreshTyped(TPCHudUIUpdate viewData, TPCHudToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _hints.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _hints.Dispose();
        }

        private void ApplyVisualState(TPCHudToolkitViewModel vm)
        {
            var panel = vm?.Panel;
            SetElementVisible(_crosshairRoot, panel?.CrosshairVisible ?? false);
            SetElementVisible(_promptRoot, !string.IsNullOrWhiteSpace(panel?.ControlPromptText));
            SetText(_prompt, panel?.ControlPromptText);
            ToolkitElementUtility.SetClass(_crosshairCenter, _highlightedClass, panel?.CrosshairHighlighted ?? false);
            _hints.ReplaceAll(vm != null ? vm.HintRows : Array.Empty<ToolkitTextRowData>());
        }
    }
}
