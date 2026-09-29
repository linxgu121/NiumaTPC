using Animancer;
using UnityEngine;


namespace NiumaTPC.Role
{
    [DisallowMultipleComponent]
    public class CharacterAnimationPlay : MonoBehaviour, ICharacterPreview
    {
        [Tooltip("模型使用的Animancer")]
        public AnimancerComponent Animancer;

        [Tooltip("出场动画")]
        public AnimationClip EnterClip;

        [Tooltip("出场动画结束后的待机动画")]
        public AnimationClip IdleClip;

        [Min(0f)]
        [Tooltip("动作混合时长")]
        public float FadeDuration = 0.25f;

        //记录本次实际使用的播放器，停止时不受 Inspector 引用变化影响
        private AnimancerComponent _activeAnimancer;

        // 保存自己绑定的事件序列，清理时无需重新创建动画状态
        private AnimancerEvent.Sequence _enterEvents;

        private bool _isShowing;

        //区分不同播放请求，防止旧结束回调影响新一轮展示
        private int _playVersion;


        private void OnDisable()
        {
            Stop();
        }


        /// <summary>
        /// 从头开始展示
        /// 配置检查失败时，不中断原本正在进行的展示
        /// (切换第二个角色再切换回来也是重新播放)
        /// </summary>
        public bool TryPlayEnter(out string error)
        {
            if (!TryValidateConfiguration(out error))
            {
                return false;
            }

            Stop();

            _activeAnimancer = Animancer;
            _isShowing = true;

            int playVersion = _playVersion;
            float fadeDuration = FadeDuration;

            // 展示动画不受游戏暂停影响，模型位置由展示挂点管理。
            _activeAnimancer.UpdateMode = AnimatorUpdateMode.UnscaledTime;
            _activeAnimancer.Animator.applyRootMotion = false;

            if (EnterClip == null)
            {
                // 首次显示直接进入待机，避免从默认姿势慢慢混入
                PlayIdle(playVersion, 0f);
                return true;
            }

            AnimancerState enterState = _activeAnimancer.Play(EnterClip);
            enterState.Time = 0f;
            enterState.Speed = 1f;

            _enterEvents = enterState.Events(this);
            _enterEvents.NormalizedEndTime = 1f;
            _enterEvents.OnEnd = () => PlayIdle(playVersion, fadeDuration);

            return true;

        }

        /// <summary>
        /// 停止展示并清理结束回调
        /// </summary>
        public bool Stop()
        {
            bool wasShowing = _isShowing;

            _isShowing = false;
            _playVersion = unchecked(_playVersion + 1);

            ClearEnterCallback();

            if (_activeAnimancer != null)
            {
                // 该 Animancer 由本组件独占，可以停止全部展示动画。
                _activeAnimancer.Stop();
            }

            _activeAnimancer = null;
            return wasShowing;
        }

        /// <summary>
        /// 播放待机动画
        /// </summary>
        private void PlayIdle(int playVersion, float fadeDuration)
        {
            // 即使旧事件已经进入待执行队列，也不能改变当前展示。
            if (this == null || !isActiveAndEnabled ||
                !_isShowing || playVersion != _playVersion)
            {
                return;
            }

            ClearEnterCallback();

            // 展示期间资源或播放器被外部移除时，结束本次展示。
            if (_activeAnimancer == null ||
                !_activeAnimancer.isActiveAndEnabled ||
                _activeAnimancer.Animator == null ||
                !_activeAnimancer.Animator.isActiveAndEnabled ||
                IdleClip == null)
            {
                Stop();
                return;
            }

            AnimancerState idleState = _activeAnimancer.Play(IdleClip, fadeDuration);
            idleState.Time = 0f;
            idleState.Speed = 1f;

            // 后续循环由动画资源的 Loop Time 控制，不需要 Update 重播。
        }

        /// <summary>
        /// 清除动画进入回调
        /// </summary>
        private void ClearEnterCallback()
        {
            if (_enterEvents == null)
            {
                return;
            }

            // Animancer 的结束事件不是天然的一次性事件。
            _enterEvents.OnEnd = null;
            _enterEvents = null;
        }

        /// <summary>
        /// 配置检测
        /// 将相关配置校验封装到一个方法中
        /// </summary>
        private bool TryValidateConfiguration(out string error)
        {
            error = string.Empty;

            if (!isActiveAndEnabled)
            {
                error = "展示动画组件未启用。";
                return false;
            }

            if (Animancer == null || !Animancer.isActiveAndEnabled)
            {
                error = "请绑定并启用展示模型的 AnimancerComponent。";
                return false;
            }

            if (Animancer.Animator == null || !Animancer.Animator.isActiveAndEnabled)
            {
                error = "Animancer 没有绑定有效且已启用的 Animator。";
                return false;
            }

            if (IdleClip == null)
            {
                error = "没有配置展示待机动画 IdleClip。";
                return false;
            }

            if (!IdleClip.isLooping)
            {
                error = "展示待机动画未开启 Loop Time，请在动画资源中设置。";
                return false;
            }

            if (float.IsNaN(FadeDuration) ||
                float.IsInfinity(FadeDuration) ||
                FadeDuration < 0f)
            {
                error = "动作混合时长必须是大于或等于 0 的有限数值。";
                return false;
            }

            return true;
        }

    }
}
