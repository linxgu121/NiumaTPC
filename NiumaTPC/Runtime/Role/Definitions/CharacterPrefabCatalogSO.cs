using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 当前游戏的角色预制体映射配置。
    /// 配置与查询分离，运行时索引由独立服务建立。
    /// </summary>
    [CreateAssetMenu(fileName = "GameCharacterPrefabCatalog",menuName = "NiumaCharacter/PrefabCatalog")]
    public class CharacterPrefabCatalogSO : ScriptableObject
    {
        [Header("角色预制体映射")]

        [Tooltip("每个角色模板 ID 配置一条记录，不应重复。空列表表示尚未配置任何角色资源。")]
        public List<CharacterPrefabEntry> Entries = new List<CharacterPrefabEntry>();
    }
}