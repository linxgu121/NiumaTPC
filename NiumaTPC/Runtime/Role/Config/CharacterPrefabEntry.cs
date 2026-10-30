using System;
using NiumaResource;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 当前游戏中，一个角色模板对应的实体与展示资源
    /// 只保存配置，不负责查询或实例化
    ///
    /// 这里我从原本的预制体使用，改为了通过资源路径进行加载
    /// 也就是说项目开始采用AB包加载（Addressables）
    /// </summary>
    [Serializable]
    public class CharacterPrefabEntry
    {
        [Tooltip("对应 CharacterDefinitionSO 的角色模板 ID，不是列表下标")]
        public ushort CharacterId;

        [Tooltip("可操控角色的完整预制体。所需控制组件由具体适配器检查；未配置时不能用于角色生成")]
        public GameObject PlayerPrefab;

        [Tooltip("选角界面展示模型的稳定 Addressables 地址")]
        [AddressableAddress(typeof(GameObject))]
        public string PreviewAddress;
    }
}
