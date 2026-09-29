using System;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 记录选择的角色，拒绝不存在的角色ID
    /// </summary>
    public class CharacterSelectionService
    {
        private readonly ICharacterCatalogQuery _catalogQuery; 

        /// <summary>
        /// 当前角色模板ID
        /// </summary>
        public ushort? SelectedCharacterId {get; private set;}

        public CharacterSelectionService(ICharacterCatalogQuery catalogQuery)
        {
            _catalogQuery = catalogQuery ?? throw new ArgumentNullException(nameof(catalogQuery));
        }

        /// <summary>
        /// 通过角色ID进行选择
        /// </summary>
        public bool TrySelect(ushort characterId, out string error)
        {
            if(!_catalogQuery.TryGetDefinition(characterId,out CharacterDefinitionSO definition) || definition == null)
            {
                error = $"无法选择角色 {characterId}：角色不存在或角色目录尚未就绪";
                return false;
            }

            SelectedCharacterId = characterId;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 清除角色选择
        /// </summary>
        public void Clear()
        {
            SelectedCharacterId = null;
        }
    
    }
}
