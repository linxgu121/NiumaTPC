using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.Simulation
{
    /// <summary>
    /// 把固定 Tick 瞄准状态写入旧角色表现黑板。
    /// 只同步动画与 IK 所需数据，不参与权威移动计算。
    /// </summary>
    public static class CharacterAimPresentationBridge
    {
        #region Public API

        public static void Apply(
            PlayerRuntimeData data,
            bool isAiming,
            float authorityYaw,
            float authorityPitch)
        {
            if (data == null)
            {
                return;
            }

            data.IsAiming = isAiming;

            data.AuthorityYaw = Mathf.Repeat(authorityYaw, 360f);

            data.AuthorityPitch = authorityPitch;

            data.AuthorityRotation = Quaternion.Euler(
                authorityPitch,
                data.AuthorityYaw,
                0f);
        }

        #endregion
    }
}
