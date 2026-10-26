using System;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 从角色定义SO构建角色基础属性实例
    /// 
    /// 这是一个静态工厂
    /// </summary>
    public static class CharacterBaseStatsFactory
    {
        public static bool TryCreate(
            CharacterDefinitionSO definitionSO,
            out CharacterBaseStats stats,
            out string error)
        {
            stats = null;
            error = string.Empty;

            if (definitionSO == null)
            {
                error = "角色定义缺少";
                return false;
            }

            var property = definitionSO.PropertySO;

            if (property == null)
            {
                error = $"角色 {definitionSO.CharacterId} 缺少属性配置";
                return false;
            }

            try
            {
                stats = new CharacterBaseStats(
                    property.BaseMaxHealth,
                    property.WalkSpeed,
                    property.JogSpeed,
                    property.SprintSpeed);

                return true;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // 4. 捕获参数越界异常，拼接角色ID和参数名
                error = $"角色ID:{definitionSO.CharacterId}，参数[{ex.ParamName}]校验失败：{ex.Message}";
                return false;
            }
        }
    }
}
