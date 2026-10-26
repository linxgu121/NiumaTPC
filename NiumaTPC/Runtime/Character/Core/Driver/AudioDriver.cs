using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Config.PlayerSOModules;
using NiumaTPC.Character.Event;
using UnityEngine;

namespace NiumaTPC.Character.Core.Driver
{
    /// <summary>
    /// 音频驱动器
    /// 接收音效事件 → 查找音频 → 直接播放
    /// </summary>
    public sealed class AudioDriver
    {
        private readonly Transform _emitter;
        private readonly AudioSource _source;
        private readonly AudioSO _audio;

        public AudioDriver(Transform emitter, AudioSource source, AudioSO audio)
        {
             _emitter = emitter;
            _source = source;
            _audio = audio;
        }

        /// <summary>
        /// 播放音频事件
        /// </summary>
        public void Play(PlayerSfxEvent evt)
        {
            if (_audio == null || _source == null) return;

            //尝试获取随机音频，获取失败则跳过
            if (!_audio.TryPickClip(evt, out var clip) || clip == null) return;

            _source.PlayOneShot(clip);
        }
    }
}
