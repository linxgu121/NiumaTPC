using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色展示目录的运行时查询服务
    /// 保留配置顺序，并建立角色 ID 索引
    /// </summary>
    public class CharacterChooseCatalog : ICharacterShowQuery
    {
        private readonly List<CharacterShowEntry> _orderedEntries = new List<CharacterShowEntry>();
        private readonly Dictionary<ushort, CharacterShowEntry> _entriesById = new Dictionary<ushort, CharacterShowEntry>();

        public int Count => _orderedEntries.Count;

        /// <summary>
        /// 根据配置重建索引
        /// 配置在运行期间按只读使用
        /// 调整配置后需要重新初始化
        /// </summary>
        public bool Initialize(CharacterChooseCatalogSO catalogSO)
        {
            Clear();

            if (catalogSO == null)
            {
                Debug.LogError("[角色展示目录] 初始化失败：未提供目录配置。");
                return false;
            }

            if (catalogSO.Entries == null)
            {
                Debug.LogError("[角色展示目录] 初始化失败：Entries 为 null。");
                return false;
            }

            for (int i = 0; i < catalogSO.Entries.Count; i++)
            {
                CharacterShowEntry entry = catalogSO.Entries[i];

                if (entry == null)
                {
                    Clear();
                    Debug.LogError($"[角色展示目录] 初始化失败：索引 {i} 的条目为空。");
                    return false;
                }

                if (_entriesById.ContainsKey(entry.CharacterId))
                {
                    Clear();
                    Debug.LogError($"[角色展示目录] 初始化失败：CharacterId={entry.CharacterId} 重复。");
                    return false;
                }

                // 两个容器引用同一条配置：列表保序，字典按 ID 查询。
                _orderedEntries.Add(entry);
                _entriesById.Add(entry.CharacterId, entry);
            }

            return true;
        }

        public bool TryGetByIndex(int index, out CharacterShowEntry entry)
        {
            entry = null;

            if (index < 0 || index >= _orderedEntries.Count)
                return false;

            entry = _orderedEntries[index];
            return true;
        }

        public bool TryGetEntry(ushort characterId, out CharacterShowEntry entry)
        {
            return _entriesById.TryGetValue(characterId, out entry);
        }

        /// <summary>
        /// 只清理运行时索引，不删除或修改原始 SO 中的配置。
        /// </summary>
        public void Clear()
        {
            _orderedEntries.Clear();
            _entriesById.Clear();
        }
    }
}
