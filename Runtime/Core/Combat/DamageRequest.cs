using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Core.Combat
{
    public struct DamageRequest
    {
        /// <summary>
        /// 数值
        /// </summary>
        public float Amount;

        public DamageRequest(float amount)
        {
            Amount = amount;
        }
    }

     public interface IDamageable
    {
        /// <summary>
        /// 伤害请求
        /// </summary>
        /// <param name="request">伤害请求结构体（数值与方法）</param>
        void RequestDamage(in DamageRequest request);
    }
}