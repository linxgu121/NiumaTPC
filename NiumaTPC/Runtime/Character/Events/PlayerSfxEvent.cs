using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Character.Event
{
    /// <summary>
    /// 角色的SFX事件
    /// 与角色有关，主要是脚步声、语音、跳跃等声音
    /// 内容映射由角色的 AudioSO 决定
    /// </summary>
    public enum PlayerSfxEvent
    {
        None = 0,

        Footstep,
        Jump,
        Land,
        Roll,
        Dodge,
        Hurt,
        Death,
        Breath
    }
}