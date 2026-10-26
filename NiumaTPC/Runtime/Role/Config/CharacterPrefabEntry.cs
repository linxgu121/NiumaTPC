using System;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 当前游戏中，一个角色模板对应的实体与展示资源。
    /// 只保存配置，不负责查询或实例化。
    /// </summary>
    [Serializable]
    public class CharacterPrefabEntry
    {
        [Tooltip("对应 CharacterDefinitionSO 的角色模板 ID，不是列表下标")]
        public ushort CharacterId;

        [Tooltip("可操控角色的完整预制体。所需控制组件由具体适配器检查；未配置时不能用于角色生成")]
        public GameObject PlayerPrefab;

        [Tooltip("选角界面使用的展示预制体。保留模型和展示动画，不应带玩家输入、相机控制或网络组件。未配置时不能展示该角色预览")]
        public GameObject PreviewPrefab;
    }
}