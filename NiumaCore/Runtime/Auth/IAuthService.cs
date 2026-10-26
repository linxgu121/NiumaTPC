using System;
using System.Threading;
using System.Threading.Tasks;

namespace NiumaCore.Auth
{
    /// <summary>
    /// 客户端账号认证服务。
    /// 当前版本只使用 Access Token，过期后需要重新登录。
    /// </summary>
    public interface IAuthService
    {
        event Action<AuthState> StateChanged;

        AuthState State { get; }

        AuthToken CurrentToken { get; }

        /// <summary>
        /// 创建账号和初始仓库。
        /// 注册成功不会自动改变登录状态。
        /// </summary>
        Task<RegisterResult> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用用户名和密码登录并取得 Access Token。
        /// </summary>
        Task<LoginResult> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 使当前会话立即失效并发布 Expired 状态。
        /// 仅供已认证接口收到后端 401 时调用，普通退出应使用 Logout。
        /// </summary>
        void InvalidateSession();

        /// <summary>
        /// 清除运行时 Token 并切换至未登录状态。
        /// </summary>
        void Logout();
    }
}
