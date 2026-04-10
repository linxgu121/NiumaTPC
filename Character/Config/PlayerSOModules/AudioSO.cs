using System;
using System.Collections.Generic;
using NiumaTPC.Item.Event;
using UnityEngine;

namespace NiumaTPC.Item.Config.PlayerSOModules
{
    /// <summary>
    /// 角色音频配置（开源版极简框架）：
    /// 只做“事件 -> 音频集合”的映射
    /// 播放策略交给 AudioDriver/AudioController（当前：随机选一个并 PlayOneShot）
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSO", menuName = "NiumaTPC/Player/Modules/AudioSO")]
    public sealed class AudioSO : ScriptableObject
    {
        [Serializable]
        public struct EventEntry
        {
            public PlayerSfxEvent Event;
            [Tooltip("该事件可用的音频集合(会随机播放)")]
            public AudioClip[] Clips;

        }

        [Header("玩家音效映射（事件 → 音效组）")]
        [SerializeField]
        private List<EventEntry> _entries = new List<EventEntry>();

        private Dictionary<PlayerSfxEvent,AudioClip[]> _cache;

        private void OnEnable() => BuildCache();

        private void OnValidate() => BuildCache();


        /// <summary>
        /// 构建缓存：把面板配置的 List 转换成字典Dictionary
        /// </summary>
        private void BuildCache()
        {
            if (_cache == null) _cache = new Dictionary<PlayerSfxEvent, AudioClip[]>();
            else _cache.Clear();

            if (_entries == null) return;
            
            //// 遍历所有配置项，装进字典
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Clips == null || e.Clips.Length == 0) continue;

                // 后写覆盖前写：便于在 Inspector 里快速覆盖
                _cache[e.Event] = e.Clips;
            }
        }

        /// <summary>
        /// 尝试获取某个事件对应的全部音效数组
        /// 安全获取，不会报错
        /// </summary>
        public bool TryGetClips(PlayerSfxEvent evt, out AudioClip[] clips)
        {
            clips = null;

            // 缓存没构建，先构建
            if (_cache == null) BuildCache();

            return _cache != null && _cache.TryGetValue(evt, out clips) && clips != null && clips.Length > 0;
        }

        /// <summary>
        /// 尝试获取某个事件对应的随机一个音效
        /// 外部播放音效
        /// </summary>
        public bool TryPickClip(PlayerSfxEvent evt, out AudioClip clip)
        {
            clip = null;
            if (!TryGetClips(evt, out var clips)) return false;

            //随机选一个
            int idx = UnityEngine.Random.Range(0, clips.Length);
            clip = clips[idx];
            //防止拿到空音效
            return clip != null;
        }
    }
}
