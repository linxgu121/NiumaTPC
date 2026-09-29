using UnityEngine;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 角色动画展示能力
    /// </summary>
    public interface ICharacterPreview
    {
        /// <summary>
        /// 提供出场动画播放能力
        /// 先播放出场动画，再循环播放结束的待机动画
        /// </summary>
        bool TryPlayEnter(out string error);

       /// <summary>
       /// 提供停止出场动画的能力
       /// </summary>
       /// <returns></returns>
        bool Stop();
    }
}
