using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 提供角色展示配置查询能力
    /// </summary>
    public interface ICharacterShowQuery
    {
        /// <summary>
        /// 当前展示目录中的条目数量
        /// </summary>
        int Count {get;}

        /// <summary>
        /// 按配置顺序查询
        /// (这里根据配置下标查询)
        /// </summary>
        bool TryGetByIndex(int index, out CharacterShowEntry entry);

        /// <summary>
        /// 根据角色模板 ID 查询展示配置
        /// </summary>
        bool TryGetEntry(ushort characterId, out CharacterShowEntry entry);

    }
}
