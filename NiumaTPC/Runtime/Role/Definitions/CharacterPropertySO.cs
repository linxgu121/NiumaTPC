using Unity.Properties;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace NiumaTPC.Role
{
    [CreateAssetMenu(fileName = "CharacterProperty",menuName = "NiumaCharacter/Character/Property/XXXProperty")]
    public class CharacterPropertySO : ScriptableObject
    {
        [Header("角色基础属性")]
        [Tooltip("最大基础血量")]
        public float BaseMaxHealth = 100f;

        [Tooltip("行走速度m/s")]
        public float WalkSpeed = 2f;

        [Tooltip("慢跑速度m/s")]
        public float JogSpeed = 3.3f;

        [Tooltip("冲刺速度m/s")]
        public float SprintSpeed = 5f;

    }
}
