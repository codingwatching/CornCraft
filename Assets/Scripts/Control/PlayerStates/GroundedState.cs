#nullable enable
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CraftSharp.Control
{
    public class GroundedState : IPlayerState
    {
        private const float SPRINT_STAMINA_START_MIN = 5F;
        private const float SPRINT_STAMINA_STOP = 1F;

        private bool _sprintRequested = false;

        public void UpdateMain(ref Vector3 currentVelocity, float interval, PlayerActions inputData, PlayerStatus info, PlayerController player)
        {
            var ability = player.AbilityConfig;

            // Reset gliding state
            info.Gliding = false;

            // If in liquid, disable sprinting/sneaking
            if (info.InLiquid)
            {
                info.Sprinting = false;
                info.Sneaking = false;
            }
            else
            {
                bool sneakPressed = inputData.Locomotion.Descend.IsPressed();

                if (sneakPressed)
                {
                    info.Sneaking = true;
                }
                // When disabling sneaking, check if enough space is free above to stand up
                else if (info.CeilingDistFromHead >= 0.3F)
                {
                    info.Sneaking = false;
                }
            }

            // Movement velocity update
            Vector3 moveVelocity;

            if (info.JumpRequested) // Jump
            {
                // Update current yaw to target yaw, immediately
                info.CurrentVisualYaw = info.TargetVisualYaw;

                var moveSpeed = info.Sprinting ? ability.SprintSpeed : info.Sneaking ? ability.SneakSpeed : ability.WalkSpeed;

                // Apply vertical and horizontal velocity
                moveVelocity = info.Moving ? player.GetMovementOrientation() * Vector3.forward * moveSpeed : currentVelocity * 0.5F;
                moveVelocity.y = 9F;

                // Debug.Log($"Grounded jump: Ground dist: {info.GroundDistFromFeet}, Velocity: {player.CurrentVelocity}");
                
                // Reset jump time
                info.JumpTime = 0;

                info.JumpRequested = false;
            }
            else // Stay on ground
            {
                // Update moving status
                info.Moving = inputData.Locomotion.Movement.IsPressed();

                if (info.Moving) // Moving
                {
                    // Initiate sprinting check
                    if (info.Moving && !info.Sneaking && _sprintRequested && info.StaminaLeft > SPRINT_STAMINA_START_MIN)
                    {
                        info.Sprinting = true;
                    }

                    if (info.Sprinting)
                    {
                        if (!inputData.Locomotion.Sprint.IsPressed() || info.StaminaLeft <= SPRINT_STAMINA_STOP)
                        {
                            info.Sprinting = false;
                        }
                    }

                    var moveSpeed = info.Sprinting ? ability.SprintSpeed : info.Sneaking ? ability.SneakSpeed : ability.WalkSpeed;
                    
                    if (info.InLiquid)
                    {
                        moveSpeed = ability.SwimSpeed;
                    }
                    
                    _sprintRequested = false;

                    // Smooth rotation for player model
                    info.CurrentVisualYaw = Mathf.MoveTowardsAngle(Mathf.LerpAngle(info.CurrentVisualYaw,
                            info.TargetVisualYaw, 0.2F), info.TargetVisualYaw, ability.TurnSpeed * interval);
                    
                    // Use target orientation to calculate actual movement direction, taking ground shape into consideration
                    // Use current horizontal velocity to smoothly reach target direction/speed
                    var targetMoveVelocity = player.GetMovementOrientation() * Vector3.forward * moveSpeed;
                    var currentHorizontalVelocity = currentVelocity;
                    currentHorizontalVelocity.y = 0F;

                    moveVelocity = Vector3.Lerp(currentHorizontalVelocity, targetMoveVelocity, 0.5F);
                }
                else // Idle or braking
                {
                    // Reset sprinting state
                    info.Sprinting = false;
                    
                    var currentHorizontalVelocity = currentVelocity;
                    currentHorizontalVelocity.y = 0F;
                    moveVelocity = Vector3.Lerp(currentHorizontalVelocity, Vector3.zero, 0.5F);
                    moveVelocity.y = 0F;
                    
                    _sprintRequested = false;
                }
            }
            
            // Apply gravity
            moveVelocity += Physics.gravity * (info.GravityScale * 3F * interval);

            currentVelocity = moveVelocity;

            // Stamina update
            if (info.Sprinting)
            {
                // Consume stamina
                info.StaminaLeft = Mathf.MoveTowards(info.StaminaLeft, 0F, interval * ability.SprintStaminaCost);
            }
            else
            {
                // Restore stamina
                info.StaminaLeft = Mathf.MoveTowards(info.StaminaLeft, ability.MaxStamina, interval * ability.StaminaRestore);
            }
        }

        public bool ShouldEnter(PlayerActions inputData, PlayerStatus info)
        {
            return !info.Spectating && info.Grounded && !info.Clinging && !info.Floating;
        }

        public bool ShouldExit(PlayerActions inputData, PlayerStatus info)
        {
            return info.Spectating || !info.Grounded || info.Clinging || info.Floating;
        }

        private Action<InputAction.CallbackContext>? jumpRequestCallback;
        private Action<InputAction.CallbackContext>? sneakRequestCallback;
        private Action<InputAction.CallbackContext>? sprintRequestCallback;

        public void OnEnter(IPlayerState prevState, PlayerStatus info, PlayerController player)
        {
            if (info.GroundDistFromFeet > 0.6F)
            {
                info.Sprinting = false;
                info.Sneaking = false;
            }

            // Reset request flags
            // info.JumpRequested = false; // Might be set in AirborneState
            _sprintRequested = player.Actions.Locomotion.Sprint.IsPressed();

            player.Actions.Locomotion.Jump.performed += jumpRequestCallback = _ =>
            {
                // Set jump flag
                info.JumpRequested = true;
            };

            player.Actions.Locomotion.Sprint.performed += sprintRequestCallback = _ =>
            {
                if (player.IsUsingAimingCamera() && Mathf.Abs(
                    Mathf.DeltaAngle(info.CurrentVisualYaw, info.MovementInputYaw)) > 30F)
                {
                    return; // Aiming and not moving forward
                }

                // Set sprint flag
                _sprintRequested = true;
            };
            
            // Debug.Log($"Enter grounded state. Velocity: {player.CurrentVelocity}");
        }

        public void OnExit(IPlayerState nextState, PlayerStatus info, PlayerController player)
        {
            if (info.GroundDistFromFeet > 0.6F)
            {
                info.Sprinting = false;
                info.Sneaking = false;
            }

            // Unregister input action events
            player.Actions.Locomotion.Jump.performed -= jumpRequestCallback;
            player.Actions.Locomotion.Sprint.performed -= sprintRequestCallback;
            
            // Debug.Log($"Exit grounded state. Velocity: {player.CurrentVelocity}");
        }

        public override string ToString() => "Grounded";
    }
}