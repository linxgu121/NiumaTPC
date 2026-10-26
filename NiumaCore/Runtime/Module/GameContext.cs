using System;
using System.Collections.Generic;
using NiumaCore.Config;
using NiumaCore.Event;
using NiumaCore.Input;
using NiumaCore.Auth;
using NiumaCore.Network;
using NiumaCore.Save;

namespace NiumaCore.Module
{
    /// <summary>
    /// 定义游戏上下文类，用于在模块之间传递和共享游戏状态和信息
    /// 游戏上下文包含了游戏的核心服务和资源，例如事件总线、配置服务、保存服务、输入阻止器等，模块可以通过游戏上下文访问这些服务和资源，以便在游戏逻辑中进行相应的处理和决策
    /// 游戏上下文还可以包含其他模块需要共享的状态和信息，例如当前的游戏阶段、当前的关卡信息、当前的角色状态等
    /// 以便模块可以根据这些状态和信息进行相应的处理和决策，例如在角色模块中根据当前的角色状态进行相应的处理，在关卡模块中根据当前的关卡信息进行相应的处理等
    /// </summary>
    public sealed class GameContext
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 事件总线
        /// </summary>
        public IEventBus EventBus { get; }
        /// <summary>
        /// 配置服务
        /// </summary>
        public IConfigService ConfigService { get; }
        /// <summary>
        /// 输入阻止器，用于在特定情况下阻止玩家的输入，例如在对话框打开时阻止玩家的移动输入，在战斗中阻止玩家的技能输入等，以确保游戏逻辑的正确性和玩家体验的一致性
        /// </summary>
        public IGameplayInputBlocker GameplayInputBlocker { get; }

        /// <summary>
        /// 网络客户端。用于登录、云存档、排行榜等需要请求后端的模块。
        /// </summary>
        public INetworkClient NetworkClient { get; }

        /// <summary>
        /// 认证服务 用于账号注册、登录、Token 状态和登出
        /// </summary>
        public IAuthService AuthService { get; }

        /// <summary>
        /// 高层存档服务。
        /// 业务模块通过该入口保存和加载，不直接依赖本地存档或云存档底层服务。
        /// </summary>
        public ISaveService SaveService { get; }

        /// <summary>
        /// 兼容旧代码的构造函数。
        /// 后续新增服务和能力接口时，优先使用 GameContext.Builder，避免构造函数参数持续膨胀。
        /// </summary>
        public GameContext(
            IEventBus eventBus,
            IConfigService configService = null,
            ISaveService saveService = null,
            IGameplayInputBlocker gameplayInputBlocker = null,
            INetworkClient networkClient = null,
            IAuthService authService = null)
        {
            EventBus = eventBus;
            ConfigService = configService;
            SaveService = saveService;
            GameplayInputBlocker = gameplayInputBlocker;
            NetworkClient = networkClient;
            AuthService = authService;

            RegisterCoreServices();
        }

        private GameContext(Builder builder)
        {
            EventBus = builder.EventBus;
            ConfigService = builder.ConfigService;
            SaveService = builder.SaveService;
            GameplayInputBlocker = builder.GameplayInputBlocker;
            NetworkClient = builder.NetworkClient;
            AuthService = builder.AuthService;

            RegisterCoreServices();

            foreach (var pair in builder.Services)
            {
                _services[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// 创建 GameContext 构建器。
        /// 新模块需要暴露查询、命令、生命周期等能力接口时，应通过 Builder.WithService 注册，避免继续扩展构造函数参数。
        /// </summary>
        public static Builder CreateBuilder()
        {
            return new Builder();
        }

        /// <summary>
        /// 注册一个可选服务或能力接口。
        /// 例如后期任务模块可以注册 IQuestQuery、IQuestCommand、IQuestLifecycle，而不是把所有能力塞进一个 IQuestService。
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            if (service == null)
            {
                _services.Remove(typeof(T));
                return;
            }

            _services[typeof(T)] = service;
        }

        /// <summary>
        /// 注销指定服务或能力接口。
        /// 模块销毁、初始化回滚或场景切换时应优先使用该显式接口，不要用 RegisterService(null) 表达注销语义。
        /// </summary>
        public bool UnregisterService<T>() where T : class
        {
            return _services.Remove(typeof(T));
        }

        /// <summary>
        /// 尝试获取某个服务或能力接口。
        /// 调用方应依赖自己真正需要的能力接口，例如 UI 只依赖查询接口，剧情只依赖命令接口。
        /// </summary>
        public bool TryGetService<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var value) && value is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// 获取某个服务或能力接口。不存在时返回 null。
        /// </summary>
        public T GetService<T>() where T : class
        {
            return TryGetService<T>(out var service) ? service : null;
        }

        /// <summary>
        /// 统一派发延迟事件。
        /// 应由全局模块启动器在所有模块 Tick 完成后调用，不要放到某一个功能模块内部调用，避免表现事件插入业务逻辑中途造成时序混乱。
        /// </summary>
        public void DrainDeferredEvents(int maxEvents = int.MaxValue)
        {
            EventBus?.DrainDeferred(maxEvents);
        }

        private void RegisterCoreServices()
        {
            RegisterService(EventBus);
            RegisterService(ConfigService);
            RegisterService(SaveService);
            RegisterService(GameplayInputBlocker);
            RegisterService(NetworkClient);
            RegisterService(AuthService);
        }

        /// <summary>
        /// GameContext 构建器。
        /// 用于在模块越来越多时保持上下文创建代码可维护，并为能力接口拆分预留插槽。
        /// </summary>
        public sealed class Builder
        {
            internal readonly Dictionary<Type, object> Services = new();

            internal IEventBus EventBus { get; private set; }
            internal IConfigService ConfigService { get; private set; }
            internal ISaveService SaveService { get; private set; }
            internal IGameplayInputBlocker GameplayInputBlocker { get; private set; }
            internal INetworkClient NetworkClient { get; private set; }
            internal IAuthService AuthService { get; private set; }

            public Builder WithEventBus(IEventBus eventBus)
            {
                EventBus = eventBus;
                return WithService(eventBus);
            }

            public Builder WithConfigService(IConfigService configService)
            {
                ConfigService = configService;
                return WithService(configService);
            }

            public Builder WithSaveService(ISaveService saveService)
            {
                SaveService = saveService;
                return WithService(saveService);
            }

            public Builder WithGameplayInputBlocker(IGameplayInputBlocker gameplayInputBlocker)
            {
                GameplayInputBlocker = gameplayInputBlocker;
                return WithService(gameplayInputBlocker);
            }

            public Builder WithNetworkClient(INetworkClient networkClient)
            {
                NetworkClient = networkClient;
                return WithService(networkClient);
            }

            public Builder WithAuthService(IAuthService authService)
            {
                AuthService = authService;
                return WithService(authService);
            }

            /// <summary>
            /// 注册额外服务或能力接口。
            /// 同一个实现对象可以用不同接口注册多次，例如同时注册 IQuestQuery 和 IQuestCommand。
            /// </summary>
            public Builder WithService<T>(T service) where T : class
            {
                if (service == null)
                {
                    Services.Remove(typeof(T));
                    return this;
                }

                Services[typeof(T)] = service;
                return this;
            }

            public GameContext Build()
            {
                return new GameContext(this);
            }
        }
    }
}
