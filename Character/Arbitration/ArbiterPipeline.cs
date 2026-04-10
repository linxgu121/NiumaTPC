using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Item.Arbitration.Arbiters;
using UnityEngine;

namespace NiumaTPC.Item.Arbitration
{
    public class ArbiterPipeline
    {
        public LODArbiter LOD {get ; private set; }
        public HealthArbiter Health {get ; private set; }
        public ActionArbiter Action {get ; private set; }
        public StaminaArbiter Stamina { get; private set; }

        private readonly NiumaCharacterController _player;

        public ArbiterPipeline(NiumaCharacterController player)
        {
            _player  = player;
            LOD = new LODArbiter(player);
            Action = new ActionArbiter(player);
            Stamina = new StaminaArbiter(player);
        }

         public void ProcessUpdateArbiters()
        {
            Action.Arbitrate();
            Health.Arbitrate();
            Stamina.Arbitrate();

            if (_player == null || _player.EnableLODArbiter)
                LOD.Arbitrate();
        }

        public void ProcessLateUpdateArbiters()
        {
            //
        }
    }
}
