using NiumaTPC.Character;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 为固定角色预制体准备基础属性
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class FixedCharacterStatsInitializer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("该固定角色对应的 CharacterDefinitionSO")]
        private CharacterDefinitionSO _definition;

        /// <summary>
        /// 仅表示基础属性准备成功，不表示角色全部初始化完成。
        /// </summary>
        public bool IsPrepared { get; private set; }

        private void Awake()
        {
            var player = GetComponent<NiumaCharacterController>();

            if(player == null)
            {
                Debug.LogError("[角色基础属性]当前物体缺少NiumaCharacterController",this);
                return;
            }

            // 离线生成器与固定预制体组件不能同时负责同一个实例的属性注入。
            if (player.BaseStats != null)
            {
                Debug.LogError("[角色基础属性]存在重复属性初始化入口，当前角色已经准备了基础属性", this);
                return;
            }

            // 配置缺失和数值合法性由已有工厂统一检查，不在这里重复实现。
            if (!CharacterBaseStatsFactory.TryCreate(_definition, out var stats, out var error))
            {
                Debug.LogError($"[角色基础属性]构建基础属性失败：{error}", this);
                return;
            }

            // 必须先于 TPC.Awake 注入，由 TPC 检查时序和重复准备。
            if (!player.TryPrepareBaseStats(stats, out error))
            {
                Debug.LogError($"[角色基础属性]角色 {_definition.CharacterId} 注入基础属性失败：{error}",this);
                return;
            }

            IsPrepared = true;
        }
    }
}