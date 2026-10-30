using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    public class CharacterPrefabCatalog
    {
        private Dictionary<ushort, CharacterPrefabEntry> _entries = new Dictionary<ushort, CharacterPrefabEntry>();

        public bool Initialize(CharacterPrefabCatalogSO catalogSO)
        {
            Clear();

            if (catalogSO == null)
            {
                Debug.LogError("角色预制体初始化失败CharacterPrefabCatalogSO传入为null");
                return false;
            }

            if (catalogSO.Entries == null)
            {
                Debug.LogError("角色预制体初始化失败：角色列表为 null");
                return false;
            }

            for (int i = 0; i < catalogSO.Entries.Count; i++)
            {
                CharacterPrefabEntry entry = catalogSO.Entries[i];

                if (entry == null)
                {
                    Clear();
                    Debug.LogError($"角色预制体初始化失败：索引 {i} 的映射条目为空");
                    return false;
                }

                if (_entries.ContainsKey(entry.CharacterId))
                {
                    Clear();
                    Debug.LogError($"角色预制体初始化失败：存在重复的 CharacterId={entry.CharacterId}");
                    return false;
                }

                // 玩家预制体与预览地址允许分别配置
                // 缺失状态在对应查询入口中判断
                _entries.Add(entry.CharacterId, entry);
            }

            return true;

        }


        public bool TryGetPlayerPrefab(ushort characterId, out GameObject prefab)
        {
            prefab = null;

            if (!_entries.TryGetValue(characterId, out CharacterPrefabEntry entry))
            {
                return false;
            }

            if (entry.PlayerPrefab == null)
            {
                return false;
            }

            prefab = entry.PlayerPrefab;
            return true;
        }

        /// <summary>
        /// 检查角色映射是否存在有效预览地址并返回标准化结果
        /// 地址为空或只有空格时不允许提交给资源服务
        /// </summary>
        public bool TryGetPreviewAddress(ushort characterId,out string address)
        {
            address = string.Empty;

            if (!_entries.TryGetValue(characterId,out CharacterPrefabEntry entry))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.PreviewAddress))
            {
                return false;
            }

            address = entry.PreviewAddress.Trim();
            return true;
        }

        /// <summary>
        /// 清空运行时索引，不修改原始配置资产
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

    }
}
