using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Item.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Item.Input.Base
{
    /// <summary>
    /// 输入源基类
    /// 提供统一的序列化接口 支持在 Unity 编辑器中拖拽赋值
    /// 所有具体输入源都应继承此类
    /// </summary>
    public abstract class InputSourceBase : MonoBehaviour, IInputSource
    {
        [Header("输入缓冲设置")]
        [Tooltip("WASD等移动轴的防抖缓存时间(秒),用于抖动抑制")]
        public float InputFlickerBuffer = 0.05f;
        [Tooltip("动作按键的缓存缓存时间(秒)，在此时间内视为已按下，便于输入缓存")]
        public float ActionBufferTime = 0.2f;

        protected PlayerRuntimeData _runtimeData;

        protected virtual void Awake()
        {
            var player = GetComponentInParent<NiumaCharacterController>();
            if (player != null)
            {
                _runtimeData = player.RuntimeData;
            }
        }

        public abstract void FetchRawInput(ref RawInputData rawDate);

        public bool IsBlocked => _runtimeData !=null && _runtimeData.Arbitration.BlockInput;
    }
}
