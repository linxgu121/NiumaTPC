using NiumaTPC.Item.Config;
using NiumaTPC.Item.Motion.MotionEnums;
using NiumaTPC.Item.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Item.Arbitration.Arbiters
{
    public sealed class StaminaArbiter
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public StaminaArbiter(NiumaCharacterController player)
        {
            _data = player.RuntimeData;
            _config = player.Config;
        }

        public void Arbitrate()
        {
             if (_data == null || _config == null || _config.Core == null) return;

            float drainRate = GetStaminaDrainRateForState(_data.CurrentLocomotionState);

            if (drainRate > 0f)
            {
                _data.CurrentStamina -= drainRate * Time.deltaTime;

                if (_data.CurrentStamina <= 0f)
                {
                    _data.CurrentStamina = 0f;
                    _data.IsStaminaDepleted = true;
                }
            }
            else if (drainRate < 0f)
            {
                _data.CurrentStamina += (-drainRate) * Time.deltaTime;

                if (_data.CurrentStamina > _config.Core.MaxStamina * _config.Core.StaminaRecoverThreshold)
                {
                    _data.IsStaminaDepleted = false;
                }
            }

            _data.CurrentStamina = Mathf.Clamp(_data.CurrentStamina, 0f, _config.Core.MaxStamina);
        }

        private float GetStaminaDrainRateForState(LocomotionState state)
        {
            return state switch
            {
                LocomotionState.Sprint => _config.Core.StaminaDrainRate,
                LocomotionState.Walk => -_config.Core.StaminaRegenRate * _config.Core.WalkStaminaRegenMult,
                LocomotionState.Jog => -_config.Core.StaminaRegenRate,
                LocomotionState.Idle => -_config.Core.StaminaRegenRate,
                _ => 0f
            };
        }
    }
}
