using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    [CreateAssetMenu(fileName = "GameCharacterCatalogSO", menuName = "NiumaCharacter/CharacterCatalog")]
    public class CharacterCatalogSO : ScriptableObject
    {
        [Header("角色目录")]

        [Tooltip("正式登场的角色模板")]
        public List<CharacterDefinitionSO> CharacterCatalog = new List<CharacterDefinitionSO>();

    }
}
