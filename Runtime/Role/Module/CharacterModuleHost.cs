using NiumaCore.Module;
using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 负责绑定配置、创建模块和转发生命周期，不处理角色查询逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterModuleHost : MonoBehaviour
    {
        #region Inspector 配置

        [SerializeField]
        [Tooltip("角色目录")]
        private CharacterCatalogSO _catalogSO;

        [SerializeField]
        [Tooltip("角色预制体")]
        private CharacterPrefabCatalogSO _prefabCatalogSO;

        #endregion

        #region 运行状态

        private CharacterModule _module;

        public bool IsStarted => _module != null && _module.IsStarted;

        public string LastError => _module == null ? string.Empty : _module.LastError;

        #endregion

        #region Unity 生命周期

        private void OnDestroy()
        {
            // Bootstrapper 也可能先调用 Stop，重复停止是安全的。
            Stop();
        }

        #endregion

        #region 启动与停止

        /// <summary>
        /// 由 Bootstrapper 传入共享上下文，创建并启动角色模块。
        /// </summary>
        public bool TryStart(GameContext context, out string error)
        {
            // 再次启动前，先移除旧模块的服务注册。
            Stop();

            _module = new CharacterModule(_catalogSO,_prefabCatalogSO);
            _module.Initialize(context);
            _module.StartModule();

            if (!_module.IsStarted)
            {
                error = _module.LastError;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 停止模块，并释放宿主持有的模块实例。
        /// </summary>
        public void Stop()
        {
            _module?.StopModule();
            _module = null;
        }

        #endregion
    }

}
