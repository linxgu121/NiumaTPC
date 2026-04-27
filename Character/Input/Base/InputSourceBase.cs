using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Input.Base
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

        /// <summary>
        /// 外部强制设置输入阻塞（如对话系统接管时）
        /// 会覆盖仲裁器的 BlockInput 标志
        /// </summary>
        public void SetBlocked(bool blocked)
        {
            if (_runtimeData != null)
                _runtimeData.Arbitration.BlockInput = blocked;
        }
    }
}
