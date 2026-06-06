using System;
using System.Collections.Generic;
using NiumaAudio.Bridge;
using NiumaAudio.Controller;
using NiumaAudio.Data;
using NiumaAudio.Service;
using NiumaTPC.Character;
using NiumaTPC.Character.Event;
using UnityEngine;

namespace NiumaTPC.AudioBridge
{
    /// <summary>
    /// NiumaTPC 到 NiumaAudio 的角色音效桥接脚本。
    /// 建议挂在玩家角色根物体上，监听 NiumaCharacterController.OnSfxEventRequested 后播放 3D Cue。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TPCAudioBridge : MonoBehaviour
    {
        [Header("角色绑定")]
        [Tooltip("玩家角色控制器。请拖同一角色根物体上的 NiumaCharacterController；为空时会尝试在当前物体或父物体中查找。")]
        [SerializeField] private NiumaCharacterController characterController;

        [Tooltip("播放位置。为空时使用 NiumaCharacterController.transform；脚步声可以绑定脚底或角色根部附近的空物体。")]
        [SerializeField] private Transform emitter;

        [Tooltip("未手动绑定 NiumaCharacterController 时是否自动在当前物体、父物体或场景中查找。正式角色预制体建议手动绑定。")]
        [SerializeField] private bool autoFindCharacterController = true;

        [Header("音频服务")]
        [Tooltip("全局音频控制器。请拖 Bootstrap 场景 AudioRoot 上的 NiumaAudioController；为空时可自动查找。")]
        [SerializeField] private NiumaAudioController audioController;

        [Tooltip("未手动绑定 NiumaAudioController 时是否自动查找场景中的 NiumaAudioController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindAudioController = true;

        [Header("事件映射")]
        [Tooltip("PlayerSfxEvent 到 AudioCueDefinition.CueId 的映射。未配置的事件会被忽略，不影响 TPC 原有本地 AudioDriver。")]
        [SerializeField] private PlayerSfxAudioCueSet[] sfxCues = Array.Empty<PlayerSfxAudioCueSet>();

        [Tooltip("Cue 未配置 OverrideBus 时是否强制改为 Sfx 总线。角色音效通常建议开启；若 CueDefinition 已准确配置 Bus，也可以关闭。")]
        [SerializeField] private bool forceSfxBus = true;

        [Tooltip("是否使用 3D 位置播放。开启时会把角色位置传给 NiumaAudio；关闭时按 2D Cue 播放。")]
        [SerializeField] private bool playAs3D = true;

        [Header("调试")]
        [Tooltip("缺少控制器、CueId 或播放失败时是否输出警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly Dictionary<PlayerSfxEvent, PlayerSfxAudioCueSet> _cueCache =
            new Dictionary<PlayerSfxEvent, PlayerSfxAudioCueSet>();

        private readonly Dictionary<PlayerSfxEvent, float> _lastPlayTime =
            new Dictionary<PlayerSfxEvent, float>();

        private IAudioCommand _runtimeAudioCommand;
        private NiumaCharacterController _boundCharacter;

        public AudioOperationResult LastAudioResult { get; private set; }

        public void SetAudioCommand(IAudioCommand command)
        {
            _runtimeAudioCommand = command;
        }

        public void SetCharacterController(NiumaCharacterController controller)
        {
            UnbindCharacter();
            characterController = controller;
            BindCharacter();
        }

        public void SetAudioController(NiumaAudioController controller)
        {
            audioController = controller;
        }

        private void OnEnable()
        {
            BuildCache();
            BindCharacter();
        }

        private void OnDisable()
        {
            UnbindCharacter();
        }

        private void OnValidate()
        {
            BuildCache();
        }

        [ContextMenu("NiumaTPC/AudioBridge/重新绑定角色音效事件")]
        private void Rebind()
        {
            UnbindCharacter();
            BuildCache();
            BindCharacter();
        }

        private void BindCharacter()
        {
            if (_boundCharacter != null)
            {
                return;
            }

            if (!ResolveCharacterController())
            {
                Warn("未找到 NiumaCharacterController，无法监听 TPC 角色音效事件。");
                return;
            }

            _boundCharacter = characterController;
            _boundCharacter.OnSfxEventRequested += HandleSfxEvent;
        }

        private void UnbindCharacter()
        {
            if (_boundCharacter == null)
            {
                return;
            }

            _boundCharacter.OnSfxEventRequested -= HandleSfxEvent;
            _boundCharacter = null;
        }

        private void HandleSfxEvent(PlayerSfxEvent sfxEvent)
        {
            if (sfxEvent == PlayerSfxEvent.None)
            {
                return;
            }

            if (!_cueCache.TryGetValue(sfxEvent, out var cueSet) ||
                cueSet == null ||
                cueSet.Cue == null ||
                !cueSet.Cue.HasPlayableKey)
            {
                return;
            }

            if (IsCoolingDown(sfxEvent, cueSet.MinIntervalSeconds))
            {
                return;
            }

            if (!TryResolveCommand(out var command))
            {
                Warn("未找到 NiumaAudioController 或 IAudioCommand，无法播放 TPC 角色音效。");
                return;
            }

            var request = CreatePlayRequest(cueSet.Cue);
            LastAudioResult = command.PlayCue(request);
            MarkPlayed(sfxEvent);

            if (LastAudioResult != null && !LastAudioResult.Succeeded)
            {
                Warn($"TPC 角色音效播放失败：{LastAudioResult.FailureReason} {LastAudioResult.Message}");
            }
        }

        private AudioPlayRequest CreatePlayRequest(AudioCueBinding cue)
        {
            var position = ResolveEmitterPosition();
            var request = cue.ToPlayRequest(position, playAs3D, "NiumaTPC");

            if (forceSfxBus && !request.HasOverrideBus)
            {
                request.HasOverrideBus = true;
                request.OverrideBus = AudioBus.Sfx;
            }

            return request;
        }

        private Vector3 ResolveEmitterPosition()
        {
            if (emitter != null)
            {
                return emitter.position;
            }

            if (characterController != null)
            {
                return characterController.transform.position;
            }

            return transform.position;
        }

        private bool ResolveCharacterController()
        {
            if (characterController != null)
            {
                return true;
            }

            if (!autoFindCharacterController)
            {
                return false;
            }

            characterController = GetComponent<NiumaCharacterController>();
            if (characterController != null)
            {
                return true;
            }

            characterController = GetComponentInParent<NiumaCharacterController>();
            if (characterController != null)
            {
                return true;
            }

#if UNITY_2023_1_OR_NEWER
            characterController = FindFirstObjectByType<NiumaCharacterController>();
#else
            characterController = FindObjectOfType<NiumaCharacterController>();
#endif
            return characterController != null;
        }

        private bool TryResolveCommand(out IAudioCommand command)
        {
            var resolved = AudioBridgeResolver.TryResolveCommand(
                _runtimeAudioCommand,
                null,
                audioController,
                autoFindAudioController,
                out command,
                out var resolvedController);

            if (resolvedController != null)
            {
                audioController = resolvedController;
            }

            return resolved;
        }

        private bool IsCoolingDown(PlayerSfxEvent sfxEvent, float minInterval)
        {
            if (minInterval <= 0f)
            {
                return false;
            }

            return _lastPlayTime.TryGetValue(sfxEvent, out var lastTime) &&
                   Time.unscaledTime - lastTime < minInterval;
        }

        private void MarkPlayed(PlayerSfxEvent sfxEvent)
        {
            _lastPlayTime[sfxEvent] = Time.unscaledTime;
        }

        private void BuildCache()
        {
            _cueCache.Clear();

            if (sfxCues == null)
            {
                return;
            }

            for (var i = 0; i < sfxCues.Length; i++)
            {
                var cueSet = sfxCues[i];
                if (cueSet == null || cueSet.Event == PlayerSfxEvent.None)
                {
                    continue;
                }

                _cueCache[cueSet.Event] = cueSet;
            }
        }

        private void Warn(string message)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"[TPCAudioBridge] {message}", this);
            }
        }
    }
}
