using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.State.UpperBody;
using UnityEngine;

namespace NiumaTPC.Character.State.Core.Base.UpperBody
{
    public abstract class UpperBodyInterceptorSO : ScriptableObject
    {
        public abstract bool TryIntercept(NiumaCharacterController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState);
    }

}
