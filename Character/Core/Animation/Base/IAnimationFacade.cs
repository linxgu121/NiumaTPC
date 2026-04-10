using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

namespace NiumaTPC.Item.Core.Animation.Base
{
    public interface IAnimationFacade
    {
        /// <summary>
        /// 动画已播放时间
        /// </summary>
        float CurrentTime {get; }
        /// <summary>
        /// 时间归一化（0为开始，1为结束）
        /// </summary>
        float CurrentNormalizedTime { get; }
        /// <summary>
        /// 播放动画片段
        /// </summary>
        /// <param name="clip">动画片段</param>
        /// <param name="options">动画播放配置</param>
        void PlayClip(AnimationClip clip, AnimPlayOptions options);
        /// <summary>
        /// 播放动画过渡/动画状态
        /// </summary>
        /// <param name="transitionObj">过度对象</param>
        /// <param name="options">配置</param>
        void PlayTransition(object transitionObj, AnimPlayOptions options);
        /// <summary>
        /// 设置指定动画层的二维混合参数
        /// </summary>
        void SetMixerParameter(Vector2 parameter, int layerIndex = 0);
        /// <summary>
        /// 播放结束后要执行的逻辑
        /// </summary>
        void SetOnEndCallback(Action onEndAction, int layerIndex = 0);
        /// <summary>
        /// 清除指定动画层的结束回调
        /// </summary>
        /// <param name="layerIndex">层级</param>
        void ClearOnEndCallback(int layerIndex = 0);
        /// <summary>
        /// 强制覆盖所有动画结束逻辑
        /// </summary>
        void SetOverrideOnEndCallback(Action onEndAction);
        /// <summary>
        /// 清除全局覆盖的动画结束回调
        /// </summary>
        void ClearOverrideOnEndCallback();
        /// <summary>
        /// 设置动画层的权重
        /// </summary>
        /// <param name="layerIndex">动画层</param>
        /// <param name="weight">权重</param>
        /// <param name="fadeDuration">平滑过渡</param>
        void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);
        /// <summary>
        /// 给指定动画层设置 Avatar 身体遮罩
        /// </summary>
        /// <param name="layerIndex">动画层</param>
        /// <param name="mask">遮罩文件</param>
        void SetLayerMask(int layerIndex, AvatarMask mask);
        /// <summary>
        /// 在动画的指定归一化时间点添加回调事件
        /// </summary>
        /// <param name="normalizedTime">动画进度</param>
        /// <param name="callback">到达该时间点要执行的方法</param>
        /// <param name="layerIndex">动画层，默认第0层</param>
        void AddCallback(float normalizedTime, Action callback, int layerIndex = 0);
        /// <summary>
        /// 获取指定动画层已播放事件
        /// </summary>
        /// <param name="layerIndex">动画层</param>
        float GetLayerTime(int layerIndex);
        /// <summary>
        /// 获取指定动画层的归一化时间（播放进度百分比）
        /// </summary>
        /// <param name="layerIndex">动画层</param>
        float GetLayerNormalizedTime(int layerIndex);
        /// <summary>
        /// 播放一段优先级最高的全身动作，强制打断当前所有动画
        /// </summary>
        /// <param name="clip">动画片段</param>
        /// <param name="fadeDuration">过度时间</param>
        void PlayFullBodyAction(AnimationClip clip, float fadeDuration = 0.2f);
        /// <summary>
        /// 停止全身强制动作
        /// </summary>
        void StopFullBodyAction();

        
    }
}
