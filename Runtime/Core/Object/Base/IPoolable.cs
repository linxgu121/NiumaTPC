using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Core.Object.Base
{
    /// <summary>
    /// 对象池接口，被池化对象用此接口复位内部状态
    /// 对象可能包含多个组件实现该接口（含子物体）对象池会全部调用
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 从对象池拿取
        /// </summary>
        void OnSpawned();
        /// <summary>
        /// 放回对象池
        /// </summary>
        void OnDespawned();
    }
}
