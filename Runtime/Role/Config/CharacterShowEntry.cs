using System;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 单个角色的展示配置
    /// </summary>
    [Serializable]
    public class CharacterShowEntry
    {
        [Tooltip("角色模板ID")]
        public ushort CharacterId;

        [Tooltip("角色的展示图/头像")]
        public Sprite Portrair;

        [Tooltip("角色简介(不填不显示)")]
        public string DescriptioKey;

        [Tooltip("选择描边")]
        public Color AccentColor = Color.white;
    }
}
