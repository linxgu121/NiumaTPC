using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色选择的配置目录
    /// 列表顺序就是卡片显示顺序，运行时查询由独立服务负责
    /// </summary>
    [CreateAssetMenu(fileName = "GameCharacterChooseCatalog", menuName = "NiumaCharacter/ChooseCatalog")]
    public class CharacterChooseCatalogSO : ScriptableObject
    {
        [Header("角色选择配置")]

        [Tooltip("角色展示配置")]
        public List<CharacterShowEntry> Entries = new List<CharacterShowEntry>();
    }
}
