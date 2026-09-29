using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Role
{
    public class CharacterCatalog
    {
        public Dictionary<ushort, CharacterDefinitionSO> _inquireDict = new Dictionary<ushort, CharacterDefinitionSO>();


        /// <summary>
        /// 初始化
        /// </summary>
        public bool Initialize(CharacterCatalogSO catalogSO)
        {

            //每次初始化清空久字典
            _inquireDict.Clear();

            //空引用判断
            if(catalogSO == null)
            {
                Debug.LogError("角色目录初始化失败CharacterCatalogSO 传入为null");
                return false;
            }

            if (catalogSO.CharacterCatalog == null)
            {
                Debug.LogError("角色目录初始化失败：角色列表为 null");
                // 不保留失败前已加入的部分数据。
                _inquireDict.Clear();
                return false;
            }

            for(int i = 0; i < catalogSO.CharacterCatalog.Count; i++)
            {
               CharacterDefinitionSO definition = catalogSO.CharacterCatalog[i];

                if( definition == null)
                {
                    Debug.LogWarning($"角色目录列表索引 {i} 的角色引用为空");
                    _inquireDict.Clear();
                    return false;
                }

                ushort characterId = definition.CharacterId;

                if(_inquireDict.ContainsKey(characterId))
                {
                    Debug.LogWarning("存在相同ID角色,请查看角色目录");
                    _inquireDict.Clear();
                    return false;
                }

                _inquireDict.Add(characterId,definition);
            }

            return true;

        }

        /// <summary>
        /// 查询
        /// </summary>
        public bool TryGetDefinition(ushort characterId, out CharacterDefinitionSO definition)
        {
            return _inquireDict.TryGetValue(characterId, out definition);
        }

    }
}
