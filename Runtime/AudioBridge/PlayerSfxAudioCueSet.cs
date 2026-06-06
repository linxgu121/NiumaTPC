using System;
using NiumaAudio.Bridge;
using NiumaTPC.Character.Event;
using UnityEngine;

namespace NiumaTPC.AudioBridge
{
    /// <summary>
    /// 单个玩家音效事件到 AudioCue 的映射。
    /// </summary>
    [Serializable]
    public sealed class PlayerSfxAudioCueSet
    {
        [Tooltip("TPC 角色音效事件。例：Footstep=脚步，Jump=跳跃，Land=落地，Hurt=受伤。")]
        public PlayerSfxEvent Event;

        [Tooltip("该事件播放的全局音频 Cue。CueId 填 AudioCueDefinition.CueId；普通角色音效建议在 CueDefinition 中配置 Bus=Sfx。")]
        public AudioCueBinding Cue = new AudioCueBinding
        {
            SourceModule = "NiumaTPC"
        };

        [Min(0f)]
        [Tooltip("该事件的最小播放间隔，单位秒。脚步声可填 0.05~0.12 防止动画事件过密；0 表示不限制。")]
        public float MinIntervalSeconds;
    }
}
