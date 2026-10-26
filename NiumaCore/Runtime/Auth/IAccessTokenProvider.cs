namespace NiumaCore.Auth
{
    /// <summary>
    /// 为需要认证的基础设施提供当前可用 Access Token。
    /// 网络层只能读取，不能修改登录状态。
    /// </summary>
    public interface IAccessTokenProvider
    {
        bool TryGetAccessToken(out string accessToken);
    }
}