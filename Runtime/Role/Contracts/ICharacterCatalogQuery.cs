namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色目录查询接口
    /// </summary>
    public interface ICharacterCatalogQuery
    {
        /// <summary>
        /// 获取角色模板信息
        /// </summary>
        bool TryGetDefinition(ushort characterId, out CharacterDefinitionSO definition);
    }
}
