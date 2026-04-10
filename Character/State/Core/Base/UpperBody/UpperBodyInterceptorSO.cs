using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Item.State.UpperBody;
using UnityEngine;

namespace NiumaTPC.Item.State.FullBody.Base.UpperBody
{
    public abstract class UpperBodyInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(NiumaCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState);
    }

}
