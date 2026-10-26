using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色定义
    /// (用于记录角色ID、名称属性的)
    /// </summary>
    [CreateAssetMenu(fileName = "XXXConfig",menuName = "NiumaCharacter/Character/CharacterConfig")]
    public class CharacterDefinitionSO : ScriptableObject
    {
        [Header("角色模板配置")]
        
        [Tooltip("角色模板ID")]
        public ushort CharacterId;

        [Tooltip("本地化名称")]
        public string DisplayNameKey;

        [Tooltip("角色职业")]
        public CharacterType CharacterType;

        [Tooltip("角色基础属性")]
        public CharacterPropertySO PropertySO;
    
    }
}
