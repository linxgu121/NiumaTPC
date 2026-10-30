using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 当前游戏的角色运行实体与展示资源地址映射配置
    /// 配置与查询分离，运行时索引由独立服务建立
    /// </summary>
    [CreateAssetMenu(fileName = "GameCharacterPrefabCatalog", menuName = "NiumaCharacter/PrefabCatalog")]
    public class CharacterPrefabCatalogSO : ScriptableObject
    {
        [Header("角色资源映射")]

        [Tooltip("每个角色模板 ID 配置一条记录，不应重复 PlayerPrefab 用于网络角色生成，PreviewAddress 用于异步加载展示模型")]
        public List<CharacterPrefabEntry> Entries = new List<CharacterPrefabEntry>();
    }
}