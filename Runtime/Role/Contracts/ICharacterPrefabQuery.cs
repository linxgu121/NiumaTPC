using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 提供角色预制体查询，不负责实例化或修改配置
    /// </summary>
    public interface ICharacterPrefabQuery
    {
        /// <summary>
        /// 查询可操控角色预制体
        /// </summary>
        bool TryGetPlayerPrefab(ushort characterId, out GameObject prefab);

        /// <summary>
        /// 查询展示角色预制体
        /// </summary>
        bool TryGetPreviewPrefab(ushort characterId, out GameObject prefab);
    }
}
