using System;
using System.Text;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using NiumaTPC.Module;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.SaveBridge
{
    /// <summary>
    /// NiumaTPC 存档桥接器。
    /// 负责把玩家位置、旋转和当前场景 ID 转换为 NiumaSave 的 Section 数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaTPCSaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string TpcSectionId = "tpc";
        private const string TpcSectionVersion = "1";
        private const string TpcSectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("玩家模块控制器。请拖入场景中的 PlayerModuleController，导出和导入位姿都会通过它完成。")]
        [SerializeField] private PlayerModuleController playerController;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。调试阶段可以开启，正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindReferences = true;

        [Header("导入行为")]
        [Tooltip("读档导入时，如果存档场景 ID 与当前场景一致，是否立即应用玩家位置和旋转。跨场景读档应先由场景流程切换场景，再调用 TryApplyPendingSnapshot。")]
        [SerializeField] private bool applyOnImportWhenSceneMatches = true;

        [Tooltip("场景 ID 不一致时是否把位姿缓存为 Pending。关闭后会直接返回导入失败。")]
        [SerializeField] private bool keepPendingSnapshotWhenSceneDiffers = true;

        private bool _registeredToSaveController;
        private int _revision;
        private TpcSaveData _pendingSnapshot;

        /// <summary>
        /// TPC 模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => TpcSectionId;

        /// <summary>
        /// TPC 存档段结构版本。
        /// </summary>
        public string SectionVersion => TpcSectionVersion;

        /// <summary>
        /// TPC 存档段修订号。
        /// 玩家移动每帧都在变化，这里不把位置变化当作自动脏标记来源，避免存档系统每帧 dirty。
        /// </summary>
        public long Revision => _revision;

        /// <summary>
        /// 是否存在等待场景加载完成后应用的玩家位姿快照。
        /// </summary>
        public bool HasPendingSnapshot => _pendingSnapshot != null;

        /// <summary>
        /// Pending 快照要求的场景 ID。为空表示没有待应用快照。
        /// </summary>
        public string PendingSceneId => _pendingSnapshot != null ? _pendingSnapshot.SceneId : null;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            UnregisterFromSaveController();
        }

        /// <summary>
        /// 导出玩家当前场景、位置与旋转。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            var playerTransform = ResolvePlayerTransform();
            if (playerTransform == null)
            {
                throw new InvalidOperationException("NiumaTPCSaveAdapter 缺少 PlayerModuleController 或玩家 Transform，无法导出 TPC 存档。");
            }

            var position = playerTransform.position;
            var rotation = playerTransform.rotation;
            var saveData = new TpcSaveData
            {
                SceneId = GetCurrentSceneId(),
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                RotationX = rotation.x,
                RotationY = rotation.y,
                RotationZ = rotation.z,
                RotationW = rotation.w
            };

            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = TpcSectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 导入玩家位姿。
        /// 如果存档场景与当前场景不同，本桥接器只缓存 Pending，不主动切换场景。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "TPC 存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"TPC 存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.SectionVersion, SectionVersion, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.VersionUnsupported,
                    $"TPC 存档段版本不支持：{section.SectionVersion}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"TPC 存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "TPC 存档段数据为空。");
            }

            try
            {
                var bytes = Convert.FromBase64String(section.EncodedData);
                var json = Encoding.UTF8.GetString(bytes);
                var saveData = JsonUtility.FromJson<TpcSaveData>(json);
                if (saveData == null)
                {
                    return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "TPC 存档段解析结果为空。");
                }

                return ImportSnapshot(saveData);
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"TPC 存档段解析失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 尝试应用跨场景读档时缓存的玩家位姿。
        /// 场景加载流程应在目标场景加载完成后调用该方法。
        /// </summary>
        public bool TryApplyPendingSnapshot()
        {
            if (_pendingSnapshot == null)
            {
                return false;
            }

            if (!IsSnapshotForCurrentScene(_pendingSnapshot))
            {
                return false;
            }

            var snapshot = _pendingSnapshot;
            _pendingSnapshot = null;
            return TryApplySnapshot(snapshot);
        }

        /// <summary>
        /// 清除跨场景读档缓存，通常用于取消读档或切换到别的存档流程。
        /// </summary>
        public void ClearPendingSnapshot()
        {
            _pendingSnapshot = null;
        }

        [ContextMenu("NiumaTPCSave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                Debug.LogWarning("[NiumaTPCSaveAdapter] 注册 TPC 存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaTPCSave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        [ContextMenu("NiumaTPCSave/尝试应用Pending位姿")]
        private void ApplyPendingSnapshotFromContextMenu()
        {
            if (!TryApplyPendingSnapshot())
            {
                Debug.LogWarning("[NiumaTPCSaveAdapter] 当前没有可应用的 Pending 位姿，或 Pending 场景 ID 与当前场景不一致。", this);
            }
        }

        private SaveSectionImportResult ImportSnapshot(TpcSaveData saveData)
        {
            if (string.IsNullOrWhiteSpace(saveData.SceneId))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "TPC 存档缺少 SceneId。");
            }

            if (!IsSnapshotForCurrentScene(saveData))
            {
                if (!keepPendingSnapshotWhenSceneDiffers)
                {
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.ImportFailed,
                        $"TPC 存档场景与当前场景不一致：save={saveData.SceneId}, current={GetCurrentSceneId()}");
                }

                _pendingSnapshot = saveData;
                return SaveSectionImportResult.Success();
            }

            if (!applyOnImportWhenSceneMatches)
            {
                _pendingSnapshot = saveData;
                return SaveSectionImportResult.Success();
            }

            return TryApplySnapshot(saveData)
                ? SaveSectionImportResult.Success()
                : SaveSectionImportResult.Fail(SaveSectionImportErrorCode.ConfigMissing, "TPC 缺少玩家控制器，无法应用位姿。");
        }

        private bool TryApplySnapshot(TpcSaveData saveData)
        {
            ResolveReferences(true);
            if (playerController == null)
            {
                return false;
            }

            var position = new Vector3(saveData.PositionX, saveData.PositionY, saveData.PositionZ);
            var rotation = new Quaternion(saveData.RotationX, saveData.RotationY, saveData.RotationZ, saveData.RotationW);
            // 存档桥接层只恢复位姿，不改变控制启停状态。
            // 剧情、菜单、死亡复活等流程应在自己的时机统一恢复玩家控制。
            playerController.Teleport(position, rotation);

            _revision++;
            return true;
        }

        private Transform ResolvePlayerTransform()
        {
            ResolveReferences(false);
            if (playerController != null)
            {
                return playerController.PlayerTransform;
            }

            return null;
        }

        private bool IsSnapshotForCurrentScene(TpcSaveData saveData)
        {
            return string.Equals(saveData.SceneId, GetCurrentSceneId(), StringComparison.Ordinal);
        }

        private static string GetCurrentSceneId()
        {
            return SceneManager.GetActiveScene().name;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (playerController == null)
            {
#if UNITY_2023_1_OR_NEWER
                playerController = FindFirstObjectByType<PlayerModuleController>();
#else
                playerController = FindObjectOfType<PlayerModuleController>();
#endif
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && playerController == null)
            {
                Debug.LogWarning("[NiumaTPCSaveAdapter] 未找到 PlayerModuleController，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                Debug.LogWarning("[NiumaTPCSaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }
    }
}
