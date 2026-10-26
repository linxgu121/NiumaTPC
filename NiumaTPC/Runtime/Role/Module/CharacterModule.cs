using UnityEngine;
using NiumaCore.Module;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色模块入口：管理目录生命周期，并向 GameContext 提供查询能力
    /// </summary>
    public class CharacterModule : IGameModule, ICharacterCatalogQuery, ICharacterPrefabQuery, ICharacterSelection
    {
        #region 依赖与运行状态

        // 编辑器配置，只读取，不在这里修改资产。
        private readonly CharacterCatalogSO _catalogSO;
        private readonly CharacterPrefabCatalogSO _prefabCatalogSO;

        private GameContext _context;
        private CharacterCatalog _catalog;
        private CharacterPrefabCatalog _prefabCatalog;
        private CharacterSelectionService _selectionCha;

        public string ModuleName => "Niuma.Character";

        public bool IsStarted { get; private set; }

        public string LastError { get; private set; } = string.Empty;

        public ushort? SelectedCharacterId => IsStarted ? _selectionCha?.SelectedCharacterId : null;

        #endregion

        #region 构造

        public CharacterModule(
            CharacterCatalogSO catalogSO,
            CharacterPrefabCatalogSO prefabCatalogSO)
        {
            // 构造时只接收依赖，实际建表放到 Initialize。
            _catalogSO = catalogSO;
            _prefabCatalogSO = prefabCatalogSO;
        }

        #endregion

        #region 模块生命周期

        /// <summary>
        /// 准备目录，但暂不向其他模块提供服务。
        /// </summary>
        public void Initialize(GameContext context)
        {
            // 重新初始化时，先解除旧上下文中的注册。
            StopModule();

            _context = context;
            _catalog = null;
            _prefabCatalog = null;
            _selectionCha = null;
            LastError = string.Empty;

            if (_context == null)
            {
                LastError = "角色模块初始化失败：缺少 GameContext";
                return;
            }

            CharacterCatalog catalog = new CharacterCatalog();
            CharacterPrefabCatalog prefabCatalog = new CharacterPrefabCatalog();
            //CharacterSelectionService selectionCha = new CharacterSelectionService(this);

            if (!catalog.Initialize(_catalogSO))
            {
                LastError = "角色模块初始化失败：角色目录配置无效，请查看目录日志";
                return;
            }

            if (!prefabCatalog.Initialize(_prefabCatalogSO))
            {
                LastError = "角色预制体初始化失败";
                return;
            }

            // 只有建表成功，才保存为可用目录。
            _catalog = catalog;
            _prefabCatalog = prefabCatalog;
            _selectionCha = new CharacterSelectionService(this);
            //_selectionCha = selectionCha;

        }

        /// <summary>
        /// 将准备好的查询能力注册到 GameContext。
        /// </summary>
        public void StartModule()
        {
            if (IsStarted)
                return;

            if (_context == null || _catalog == null || _prefabCatalog == null || _selectionCha == null)
            {
                if (string.IsNullOrEmpty(LastError))
                    LastError = "角色模块尚未成功初始化，无法启动。";

                return;
            }

            // 不覆盖其他实例已经注册的服务。
            if (_context.TryGetService<ICharacterCatalogQuery>(out var catalogQuery)
                && !ReferenceEquals(catalogQuery, this)
                || _context.TryGetService<ICharacterPrefabQuery>(out var prefabQuery)
                && !ReferenceEquals(prefabQuery, this)
                || (_context.TryGetService<ICharacterSelection>(out var selection)
                && !ReferenceEquals(selection, this)))
            {
                LastError = "角色模块启动失败：已存在其他角色目录查询服务。";
                return;
            }

            _context.RegisterService<ICharacterCatalogQuery>(this);
            _context.RegisterService<ICharacterPrefabQuery>(this);
            _context.RegisterService<ICharacterSelection>(this);

            IsStarted = true;
            LastError = string.Empty;
        }

        /// <summary>
        /// 停止提供查询，并移除属于自己的服务注册。
        /// </summary>
        public void StopModule()
        {
            // 先关闭对外入口，再清理本次运行的选择。
            IsStarted = false;
            _selectionCha?.Clear();

            if (_context == null)
                return;

            // 多种能力分别注销，而且只移除属于当前实例的注册。
            if (_context.TryGetService<ICharacterCatalogQuery>(out var catalogQuery)
                && ReferenceEquals(catalogQuery, this))
            {
                _context.UnregisterService<ICharacterCatalogQuery>();
            }

            if (_context.TryGetService<ICharacterPrefabQuery>(out var prefabQuery)
                && ReferenceEquals(prefabQuery, this))
            {
                _context.UnregisterService<ICharacterPrefabQuery>();
            }

            if (_context.TryGetService<ICharacterSelection>(out var selection)
                && ReferenceEquals(selection, this))
            {
                _context.UnregisterService<ICharacterSelection>();
            }

        }
        public void Tick(float deltaTime)
        {
            // 目录查询不需要逐帧执行，暂时留空。
        }

        #endregion

        #region 查询入口

        public bool TryGetDefinition(ushort characterId, out CharacterDefinitionSO definition)
        {
            definition = null;

            if (!IsStarted || _catalog == null)
            {
                return false;
            }

            return _catalog.TryGetDefinition(characterId, out definition);
        }

        public bool TryGetPlayerPrefab(ushort characterId, out GameObject prefab)
        {
            prefab = null;

            if (!IsStarted || _prefabCatalog == null)
            {
                return false;
            }

            return _prefabCatalog.TryGetPlayerPrefab(characterId, out prefab);
        }

        public bool TryGetPreviewPrefab(ushort characterId, out GameObject prefab)
        {
            prefab = null;

            if (!IsStarted || _prefabCatalog == null)
            {
                return false;
            }

            return _prefabCatalog.TryGetPreviewPrefab(characterId, out prefab);
        }

        #endregion

        #region 角色选择

        public bool TrySelect(ushort characterId, out string error)
        {
            if (!IsStarted || _selectionCha == null)
            {
                error = "角色模块尚未启动，无法选择角色";
                return false;
            }

            return _selectionCha.TrySelect(characterId, out error);
        }

        public void ClearSelection()
        {
            _selectionCha?.Clear();
        }

        #endregion
    }
}
