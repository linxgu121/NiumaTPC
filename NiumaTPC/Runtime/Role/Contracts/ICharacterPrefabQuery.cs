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
        /// 查询角色预览使用的稳定 Addressables 地址
        /// 空地址或只有空格时返回失败
        /// </summary>
        bool TryGetPreviewAddress(ushort characterId,out string address);
    }
}
