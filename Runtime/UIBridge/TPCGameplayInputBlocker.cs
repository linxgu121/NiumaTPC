using NiumaTPC.Module;
using NiumaUI.Enum;
using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaTPC.UIBridge
{
    /// <summary>
    /// UI Toolkit 到 TPC 的玩法输入阻塞桥接。
    /// UIToolkitUIManager 打开会阻塞玩法输入的面板时，通过本桥接禁用玩家控制；关闭面板后释放 UI 添加的阻塞。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TPCGameplayInputBlocker : MonoBehaviour, IGameplayInputBlocker
    {
        private const string ReasonPrefix = "UI.";

        [Header("玩家控制器")]
        [Tooltip("玩家模块控制器。拖 Player 根物体上的 PlayerModuleController；不要拖模型子物体。")]
        [SerializeField] private PlayerModuleController playerController;

        [Tooltip("未手动绑定时，是否在当前物体、父物体和子物体中自动查找 PlayerModuleController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoResolvePlayerController = true;

        [Header("调试")]
        [Tooltip("缺少 PlayerModuleController 时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private void Awake()
        {
            ResolvePlayerController(false);
        }

        public void SetBlocked(bool blocked, UIMode reason)
        {
            if (!ResolvePlayerController(true))
            {
                return;
            }

            if (blocked)
            {
                ClearAllUiReasons();
                playerController.DisableControl(BuildReason(reason));
                return;
            }

            ClearAllUiReasons();
        }

        private void ClearAllUiReasons()
        {
            var values = System.Enum.GetValues(typeof(UIMode));
            for (var i = 0; i < values.Length; i++)
            {
                playerController.EnableControl(BuildReason((UIMode)values.GetValue(i)));
            }
        }

        private static string BuildReason(UIMode mode)
        {
            return ReasonPrefix + mode;
        }

        private bool ResolvePlayerController(bool logIfMissing)
        {
            if (playerController != null)
            {
                return true;
            }

            if (!autoResolvePlayerController)
            {
                Warn("未绑定 PlayerModuleController，UI 无法阻塞玩家输入。", logIfMissing);
                return false;
            }

            playerController = GetComponent<PlayerModuleController>()
                               ?? GetComponentInParent<PlayerModuleController>()
                               ?? GetComponentInChildren<PlayerModuleController>(true);

            if (playerController == null)
            {
                Warn("自动查找 PlayerModuleController 失败，UI 无法阻塞玩家输入。", logIfMissing);
            }

            return playerController != null;
        }

        private void Warn(string message, bool shouldLog)
        {
            if (logWarnings && shouldLog && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"[TPCGameplayInputBlocker] {message}", this);
            }
        }
    }
}