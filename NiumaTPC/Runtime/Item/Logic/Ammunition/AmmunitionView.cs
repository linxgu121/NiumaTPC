using NiumaTPC.Core.Object;
using UnityEngine;

namespace NiumaTPC.Item
{
    /// <summary>
    /// 子弹的纯视觉表现
    /// </summary>
    [DisallowMultipleComponent]
    public class AmmunitionView : MonoBehaviour, IPoolable
    {
        [SerializeField]
        [Tooltip("绑定当前预制体中的 TrailRenderer，可留空，留空时只显示模型")]
        private TrailRenderer trail;

        private ParticleSystem[] _particles;

        // 当前这次视觉对象租用的编号，不代表服务器 ShotId
        public ulong BindingId { get; private set; }

        public bool IsBound => BindingId != 0;

        #region Unity 生命周期

        private void Awake()
        {
            if (trail != null)
            {
                // 对象生命周期由外部管理，拖尾不能自行销毁对象
                trail.autodestruct = false;
            }

            ClearBinding();
        }

        private void OnDisable()
        {
            ClearBinding();
        }

        #endregion

        /// <summary>
        /// 为本次视觉对象租用建立绑定
        /// 从对象池取出后，由调用方立即执行
        /// </summary>
        public bool Bind(
            ulong bindingId,
            Vector3 position,
            Vector3 velocity)
        {
            if (bindingId == 0 || !isActiveAndEnabled)
            {
                return false;
            }

            // 先停止旧拖尾，再移动到新的发射位置
            ClearBinding();

            BindingId = bindingId;

            // 清除上一次复用留下的朝向
            transform.rotation = Quaternion.identity;

            ApplyTransform(position, velocity);

            // 先完成定位，再播放，避免在对象池旧位置产生粒子
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem system = _particles[i];

                if (system != null && system.gameObject.activeInHierarchy)
                {
                    // 数组已包含子粒子，不再递归播放
                    system.Play(false);
                }
            }


            if (trail != null)
            {
                // 定位后再次清空，防止连接旧位置与新起点
                trail.Clear();
                trail.emitting = true;
            }

            return true;
        }

        /// <summary>
        /// 应用外部计算好的显示位置
        /// 编号不匹配时拒绝更新，避免操作已经复用的视图
        /// </summary>
        public bool ApplyPose(
            ulong bindingId,
            Vector3 position,
            Vector3 velocity)
        {
            if (!isActiveAndEnabled ||
                !IsBound ||
                BindingId != bindingId)
            {
                return false;
            }

            ApplyTransform(position, velocity);

            return true;
        }

        private void ApplyTransform(
            Vector3 position,
            Vector3 velocity)
        {
            transform.position = position;

            // 接近静止时保留朝向，不向 LookRotation 传入零向量
            if (velocity.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 forward = velocity.normalized;

            // 接近竖直飞行时，选择与前方向不共线的上方向
            Vector3 up =
                Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f
                    ? Vector3.forward
                    : Vector3.up;

            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        #region 对象池生命周期

        public void OnSpawned()
        {
            // 对象池取出不等于已经绑定某一颗子弹
            ClearBinding();
        }

        public void OnDespawned()
        {
            ClearBinding();
        }

        private void ClearBinding()
        {
            EnsureParticles();

            BindingId = 0;

            if (trail != null)
            {
                trail.autodestruct = false;
                trail.emitting = false;
                trail.Clear();
            }

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem system = _particles[i];

                if (system != null)
                {
                    system.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
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

            // 包含失活子节点，兼容对象池预热时 Awake 尚未执行
            _particles = GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.MainModule main = _particles[i].main;

                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        #endregion

    }
}
