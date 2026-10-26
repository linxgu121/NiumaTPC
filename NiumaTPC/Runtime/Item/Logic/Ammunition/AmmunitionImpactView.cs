using NiumaTPC.Core.Object;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 命中的纯视觉特效
    /// </summary>
    [DisallowMultipleComponent]
    public class AmmunitionImpactView : MonoBehaviour, IPoolable
    {
        #region 表现配置

        [SerializeField]
        [Tooltip("绑定本预制体的根 ParticleSystem 会同时播放子粒子 留空时只显示静态模型")]
        private ParticleSystem particleRoot;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("整组命中特效保留的秒数，供外部管理者计时回收")]
        private float retainTime = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("沿表面法线向外偏移的米数；资源内部已有偏移时先保持为 0")]
        private float surfaceOffset = 0f;

        #endregion

        #region 运行状态

        private ParticleSystem[] _particles;

        public float RetainTime => retainTime;

        public bool IsPlaying { get; private set; }

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        #endregion

        /// <summary>
        /// 在真实接触点播放命中特效
        /// 调用前应已从对象池取出并完成场景归属设置
        /// </summary>
        public bool PlayAt(Vector3 point, Vector3 normal)
        {
            if (!isActiveAndEnabled ||
                normal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            // 必须先清除上次命中的粒子，再移动到新位置
            ResetPresentation();

            Vector3 surfaceNormal = normal.normalized;

            Vector3 position = point + surfaceNormal * surfaceOffset;

            // 约定预制体根节点正 Z 方向朝向表面外侧
            Quaternion rotation = Quaternion.FromToRotation(
                Vector3.forward,
                surfaceNormal);

            transform.SetPositionAndRotation(position, rotation);

            if (particleRoot != null)
            {
                particleRoot.Play(true);
            }


            IsPlaying = true;
            return true;
        }

        #region 对象池生命周期

        public void OnSpawned()
        {
            // 取出时先保持未播放，等待外部传入命中位置
            ResetPresentation();
        }

        public void OnDespawned()
        {
            ResetPresentation();
        }

        private void ResetPresentation()
        {
            EnsureParticles();

            IsPlaying = false;

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem system = _particles[i];

                if (system == null)
                {
                    continue;
                }

                // 数组已经包含子粒子，因此逐个清理即可
                system.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        #endregion

        #region 粒子初始化

        private void EnsureParticles()
        {
            if (_particles != null)
            {
                return;
            }

            // 延迟初始化也能覆盖失活预制体的对象池预热
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.MainModule main = _particles[i].main;

                // 播放时机与对象回收统一交给表现管线
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        #endregion

    }
}
