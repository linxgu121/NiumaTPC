using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Event;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Expression
{
    /// <summary>
    /// Audio 控制器：消费黑板上的音频事件意图队列 并委托给 AudioDriver 播放 
    /// </summary>
    public sealed class AudioController
    {
        private readonly NiumaCharacterController _player;
        private readonly PlayerRuntimeData _data;

        public AudioController(NiumaCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        public void Update()
        {
            if (_player == null || _data == null) return;
            
            // 仲裁标记检查：音效系统被阻断时 清空队列并返回
            if (_data.Arbitration.BlockAudio)
            {
                _data.SfxQueue.Clear();
                return;
            }
            
            int count = _data.SfxQueue.Count;
            for (int i = 0; i < count; i++)
            {
                var evt = _data.SfxQueue.GetSfxEvent(i);
                if (!_player.UseExternalSfxBridge)
                {
                    _player.AudioDriver?.Play(evt);
                }

                _player.NotifySfxEvent(evt);
            }

            _data.SfxQueue.Clear();
        }
    }
}
