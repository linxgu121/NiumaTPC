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

            if(catalogSO == null)
            {
                Debug.LogError("角色预制体初始化失败CharacterPrefabCatalogSO传入为null");
                return false;
            }

            if (catalogSO.Entries == null)
            {
                Debug.LogError("角色预制体初始化失败：角色列表为 null");
                return false;
            }

            for(int i = 0 ; i < catalogSO.Entries.Count; i++)
            {
                CharacterPrefabEntry entry = catalogSO.Entries[i];

                if(entry == null)
                {
                    Clear();
                    Debug.LogError($"角色预制体初始化失败：索引 {i} 的映射条目为空");
                    return false;
                }

                if(_entries.ContainsKey(entry.CharacterId))
                {
                    Clear();
                    Debug.LogError($"角色预制体初始化失败：存在重复的 CharacterId={entry.CharacterId}");
                    return false;
                }

                //两种资源允许分别配置，是否缺失在对应查询时判断
                _entries.Add(entry.CharacterId, entry);
            }

            return true;

        }


        public bool TryGetPlayerPrefab(ushort characterId, out GameObject prefab)
        {
            prefab = null;

            if(!_entries.TryGetValue(characterId, out CharacterPrefabEntry entry))
            {
                return false;
            }

            if(entry.PlayerPrefab == null)
            {
                return false;
            }

            prefab = entry.PlayerPrefab;
            return true;
        }

        public bool TryGetPreviewPrefab(ushort characterId, out GameObject prefab)
        {
            prefab = null;

            if (!_entries.TryGetValue(characterId, out CharacterPrefabEntry entry))
            {
                return false;
            }

            if (entry.PreviewPrefab == null)
            {
                return false;
            }

            prefab = entry.PreviewPrefab;
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
