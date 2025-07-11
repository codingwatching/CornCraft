#nullable enable
using CraftSharp.Rendering;
using KinematicCharacterController;
using UnityEngine;

namespace CraftSharp.Control
{
    public class FloatingState : IPlayerState
    {
        private bool _forceUngroundRequested = false;
        private bool _forceUngroundConfirmed = false;

        public const float THRESHOLD_CLIMB_1M = 1.1F + 0.6F;
        public const float THRESHOLD_CLIMB_UP = 0.4F + 0.6F;

        private void CheckClimbOverInLiquid(PlayerStatus info, PlayerController player)
        {
            var yawRadian = info.TargetVisualYaw * Mathf.Deg2Rad;
            var dirVector = new Vector2(Mathf.Sin(yawRadian), Mathf.Cos(yawRadian));
            var maxDist = GroundedState.DistanceToSquareSide(dirVector, player.AbilityConfig.ClimbOverMaxDist);
            
            if (info is { Moving: true, BarrierHeight: > THRESHOLD_CLIMB_UP and < THRESHOLD_CLIMB_1M } &&
                info.BarrierDistance < maxDist && info.WallDistance - info.BarrierDistance > 0.7F) // Climb up platform
            {
                if (info is { BarrierYawAngle: < 30F, YawDeltaAbs: <= 10F }) // Check if available, for high barriers check cooldown and angle
                    // Trying to moving forward
                {
                    player.ClimbOverBarrier(info.BarrierDistance, info.BarrierHeight, false, true);

                    // Prevent unground preparation if climbing is successfully initiated, or only timer is not ready
                    _forceUngroundRequested = false;
                }
            }
        }

        public void UpdateBeforeMotor(float interval, PlayerActions inputData, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            CheckClimbOverInLiquid(info, player);

            if (_forceUngroundRequested) // Force unground
            {
                if (info.Grounded)
                {
                    _forceUngroundConfirmed = true;

                    // Makes the character skip ground probing/snapping on its next update
                    motor.ForceUnground();
                    // Also reset grounded flag
                    info.Grounded = false;
                }

                _forceUngroundRequested = false;
            }
        }

        public void UpdateMain(ref Vector3 currentVelocity, float interval, PlayerActions inputData, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            var ability = player.AbilityConfig;
            
            info.Sprinting = false;
            info.Gliding = false;
            info.Flying = false;

            var swimSpeed = ability.SwimSpeed;

            Vector3 moveVelocity = Vector3.zero;

            if (_forceUngroundConfirmed)
            {
                // Apply vertical velocity to reduced horizontal velocity
                moveVelocity = currentVelocity * 0.5F + motor.CharacterUp * ability.JumpSpeedCurve.Evaluate(currentVelocity.magnitude);

                _forceUngroundConfirmed = false;
            }
            else
            {
                // Update moving status
                bool prevMoving = info.Moving;
                info.Moving = inputData.Locomotion.Movement.IsPressed();

                // Animation mirror randomization
                if (info.Moving != prevMoving)
                {
                    player.RandomizeMirroredFlag();
                }

                // Check vertical movement...
                float distToAfloat = PlayerStatusUpdater.FLOATING_DIST_THRESHOLD - 0.2F - info.LiquidDist;

                if (inputData.Locomotion.Ascend.IsPressed())
                {
                    if (distToAfloat > 0F) // Underwater
                    {
                        if(distToAfloat <= 1F) // Move up no further than top of the surface
                        {
                            moveVelocity = distToAfloat * 2f * motor.CharacterUp;
                        }
                        else // Just move up
                        {
                            moveVelocity = swimSpeed * motor.CharacterUp;
                        }

                        // Workaround: Special handling for force unground
                        if (info.Grounded && !_forceUngroundRequested)
                        {
                            _forceUngroundRequested = true;
                        }

                        
                    }
                }
                else if (inputData.Locomotion.Descend.IsPressed())
                {
                    if (!info.Grounded)
                    {
                        moveVelocity = -swimSpeed * motor.CharacterUp;
                    }
                }

                // Check horizontal movement...
                if (info.Moving)
                {
                    // Smooth rotation for player model
                    info.CurrentVisualYaw = Mathf.MoveTowardsAngle(info.CurrentVisualYaw, info.TargetVisualYaw, ability.TurnSpeed * interval * 0.5F);
                    
                    // Use target orientation to calculate actual movement direction
                    moveVelocity += player.GetMovementOrientation() * Vector3.forward * swimSpeed;
                }

                // Smooth movement (if accelerating)
                if (moveVelocity.sqrMagnitude > currentVelocity.sqrMagnitude)
                {
                    moveVelocity = Vector3.MoveTowards(currentVelocity, moveVelocity, ability.AccSpeed * interval);
                }

                // Apply gravity (nonexistent)
            }

            currentVelocity = moveVelocity;
            
            // Consume stamina (nonexistent)
        }

        public bool ShouldEnter(PlayerActions inputData, PlayerStatus info)
        {
            return !info.Spectating && info.Floating;
        }

        public bool ShouldExit(PlayerActions inputData, PlayerStatus info)
        {
            return info.Spectating || !info.Floating;
        }

        public void OnEnter(IPlayerState prevState, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            info.Sprinting = false;

            player.StartCrossFadeState(AnimatorEntityRender.TREAD_NAME);

            // Reset request flags
            _forceUngroundRequested = false;
            _forceUngroundConfirmed = false;
        }

        public void OnExit(IPlayerState nextState, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            
        }

        public override string ToString() => "Floating";
    }
}