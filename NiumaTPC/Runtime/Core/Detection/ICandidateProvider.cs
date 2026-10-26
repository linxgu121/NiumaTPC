using System.Collections.Generic;

namespace NiumaTPC.Core.Detection
{
    /// <summary>
    /// 向上层系统提供某一类型的候选集合
    /// 不暴露候选来自Trigger、空间查询还是服务器缓存
    /// </summary>
    public interface ICandidateProvider<TCandidate>
        where TCandidate : class
    {
        /// <summary>
        /// 当前保存的候选数量
        /// </summary>
        int CandidateCount { get; }

        /// <summary>
        /// 将当前候选复制到调用方提供的复用缓冲区
        /// 不负责具体业务的有效性和优先级判断
        /// </summary>
        int CopyCandidates(List<TCandidate> destination);
    }
}