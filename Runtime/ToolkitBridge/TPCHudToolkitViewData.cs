using System;
using NiumaUI.Toolkit.Common;

namespace NiumaTPC.ToolkitBridge
{
    public enum TPCHudUIUpdateType
    {
        Refresh = 0,
        Cleared = 1
    }

    [Serializable]
    public sealed class TPCHudViewData
    {
        public int Revision;
        public bool CrosshairVisible = true;
        public bool CrosshairHighlighted;
        public string ControlPromptText;
        public string[] ControlHints = Array.Empty<string>();
    }

    public readonly struct TPCHudUIUpdate
    {
        public readonly TPCHudUIUpdateType UpdateType;
        public readonly int Revision;
        public readonly TPCHudViewData PanelData;
        public readonly TPCHudViewData PreviousPanelData;

        public TPCHudUIUpdate(TPCHudUIUpdateType updateType, int revision, TPCHudViewData panelData, TPCHudViewData previousPanelData)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
        }
    }

    public sealed class TPCHudToolkitViewModel : UIPanelViewModelBase
    {
        public readonly System.Collections.Generic.List<ToolkitTextRowData> HintRows = new System.Collections.Generic.List<ToolkitTextRowData>();
        public TPCHudViewData Panel { get; private set; }
        public TPCHudUIUpdateType UpdateType { get; private set; }

        public void Apply(TPCHudUIUpdate update)
        {
            SetContext("player_hud");
            Panel = update.PanelData;
            UpdateType = update.UpdateType;
            RebuildRows();
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Panel = null;
            UpdateType = TPCHudUIUpdateType.Cleared;
            HintRows.Clear();
        }

        private void RebuildRows()
        {
            HintRows.Clear();
            var hints = Panel?.ControlHints ?? Array.Empty<string>();
            for (var i = 0; i < hints.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(hints[i]))
                    HintRows.Add(new ToolkitTextRowData($"hint:{i}", hints[i].Trim()));
            }
        }
    }
}
