using System.Collections.Generic;

namespace NiumaCore.Config
{
    /// <summary>
    /// 定义配置服务接口，用于加载和获取游戏配置数据
    /// 配置服务负责从配置文件或其他数据源加载游戏配置数据，并提供获取配置数据的功能，以便模块可以根据需要获取对应的配置数据，例如在角色模块中获取角色配置数据，在关卡模块中获取关卡配置数据等
    /// 配置数据通常以类的形式定义，包含各种属性和字段，用于存储和管理游戏中的各种配置项，例如角色属性、关卡信息、道具数据等，模块可以通过配置服务获取这些配置数据以实现相应的游戏逻辑和功能
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 获取配置数据，接受一个配置ID参数，允许模块根据配置ID获取对应的配置数据，例如在角色模块中获取角色配置数据时传入角色ID，在关卡模块中获取关卡配置数据时传入关卡ID等
        /// <summary>
        T GetConfig<T>(string id) where T : class;
        /// <summary>
        /// 获取所有配置数据，允许模块获取某个类型的所有配置数据，例如在角色模块中获取所有角色配置数据，在关卡模块中获取所有关卡配置数据等，以便模块可以根据需要获取对应的配置数据列表进行处理和使用
        /// </summary>
        IReadOnlyList<T> GetAll<T>() where T : class;
    }
}
