using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Item.State.Base;
using NiumaTPC.Item.State.FullBody.Base.UpperBody;
using NiumaTPC.Item.State.FullBody.Enum;
using NiumaTPC.Item.State.Enum;
using UnityEngine;


namespace NiumaTPC.Item.State
{
    [CreateAssetMenu(fileName ="PlayerBrain_Default", menuName = "NiumaTCP/Player/Modules/Player Brain")]
    public class PlayerBrainSO : ScriptableObject
    {
        [Header("状态装载")]
        [Tooltip("只需在下拉菜单中勾选玩家需要的状态,列表中排在第0位的将作为启动状态")]
        public List<PlayerStateType> AvailableStates = new List<PlayerStateType>();

        [Header("全局打断管线")]
        [Tooltip("将打断器SO拖入此列表,从上到下决定绝对优先级")]
        public List<StateInterceptorSO> GlobalInterceptors = new List<StateInterceptorSO>();

        [Header("上半身状态")]
        [Tooltip("列表中排在首位的将作为上半身启动状态 ")]
        public List<UpperBodyStateType> UpperBodyStates = new List<UpperBodyStateType>();

        [Tooltip("上半身专属打断管线")]
        public List<UpperBodyInterceptorSO> UpperBodyInterceptors = new List<UpperBodyInterceptorSO>();
    }
}