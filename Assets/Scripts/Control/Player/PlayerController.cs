using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using CraftSharp.Event;
using CraftSharp.Protocol;
using CraftSharp.Rendering;

namespace CraftSharp.Control
{
    [RequireComponent(typeof (PlayerStatusUpdater))]
    public class PlayerController : MonoBehaviour
    {
        // Player Config Fields
        [SerializeField] private PlayerAbilityConfig m_AbilityConfig;

        public PlayerAbilityConfig AbilityConfig => m_AbilityConfig;

        // Status Fields
        [SerializeField] private PlayerStatusUpdater m_StatusUpdater;
        public PlayerStatus Status => m_StatusUpdater.Status;

        // Camera & Player Render Fields
        private CameraController m_CameraController;
        private EntityRenderManager m_EntityRenderManager;
        private Transform m_FollowRef;
        private Transform m_AimingRef;
        private EntityRender m_PlayerRender;

        // Input System Fields & Methods
        public PlayerActions Actions { get; private set; }

        public void EnableInput() => Actions?.Enable();
        public void DisableInput() => Actions?.Disable();
        
        private Vector3 currentVelocity = Vector3.zero;

#nullable enable
        
        // Player State Fields
        private IPlayerState? pendingState;
        
#nullable disable
        
        public IPlayerState CurrentState { get; private set; } = PlayerStates.PRE_INIT;

        // Values for sending over to the server. Should only be set
        // from the unity thread and read from the network thread

        /// <summary>
        /// Player location for sending to the server
        /// </summary>
        public Location Location2Send { get; private set; }

        /// <summary>
        /// Player yaw for sending to the server
        /// This yaw value is stored in Minecraft coordinate system.
        /// Conversion is required when assigning to unity transform
        /// </summary>
        public float MCYaw2Send { get; private set; }

        /// <summary>
        /// Player pitch for sending to the server
        /// </summary>
        public float Pitch2Send { get; private set; }

        /// <summary>
        /// Grounded flag for sending to the server
        /// </summary>
        public bool IsGrounded2Send { get; private set; }

        public void SwitchPlayerRenderFromPrefab(EntitySpawnData entitySpawn, GameObject renderPrefab)
        {
            var prevRender = m_PlayerRender;

            if (prevRender)
            {
                // Unregister previous aiming mode change handler
                EventManager.Instance.Unregister<CameraAimingEvent>(prevRender.HandleAimingModeChange);
            }
            
            // Update controller's player render
            if (m_EntityRenderManager)
            {
                m_PlayerRender = m_EntityRenderManager.AddEntityRenderFromGivenPrefab(entitySpawn, renderPrefab);
                m_PlayerRender.SetClientEntityFlag();

                // Setup camera ref and use it
                m_FollowRef = m_PlayerRender.SetupCameraRef();
                m_AimingRef = m_PlayerRender.GetAimingRef();
                
                // Initialize aiming mode state
                if (m_CameraController)
                {
                    // Create a dummy aiming mode change event
                    var aimingModeEvent = new CameraAimingEvent(m_CameraController.IsAimingOrLocked);
                    m_PlayerRender.HandleAimingModeChange(aimingModeEvent);
                }
                
                EventManager.Instance.Register<CameraAimingEvent>(m_PlayerRender.HandleAimingModeChange);
            }
            else
            {
                Debug.LogWarning("Player render not found in game object!");
                // Use own transform
                m_FollowRef = transform;
                m_AimingRef = transform;
            }

            if (prevRender)
            {
                // Unload and then destroy previous render object, if present
                prevRender.Unload();
            }

            if (m_CameraController)
            {
                m_CameraController.SetTargets(m_FollowRef, m_AimingRef);
            }

            // Re-initialize current state
            ChangeToState(CurrentState);
        }

        public void SetEntityRenderManager(EntityRenderManager entityRenderManager)
        {
            m_EntityRenderManager = entityRenderManager;
        }

        public void HandleCameraControllerSwitch(CameraController cameraController)
        {
            m_CameraController = cameraController;

            if (m_FollowRef && m_AimingRef)
            {
                m_CameraController.SetTargets(m_FollowRef, m_AimingRef);
            }
            else
            {
                Debug.LogWarning("Camera ref is not present when switching to a new camera controller.");
            }
        }

#nullable enable

        private Action<GameModeUpdateEvent>? gameModeCallback;
        private Action<HealthUpdateEvent>? healthCallback;

        public delegate void ItemStackEventHandler(ItemStack? item, ItemActionType actionType);
        public event ItemStackEventHandler? OnCurrentItemChanged;
        public void ChangeCurrentItem(ItemStack? currentItem, ItemActionType actionType)
        {
            OnCurrentItemChanged?.Invoke(currentItem, actionType);
        }
        
        // Used by client to send EntityAction packets
        public delegate void PlayerActionEventHandler(EntityActionType actionType);
        public event PlayerActionEventHandler? OnPlayerAction;

#nullable disable

        private void Awake()
        {
            if (Actions != null) return;
            
            Actions = new PlayerActions();
            Actions.Enable();
        }

        private void Start()
        {
            // Set stamina to max value
            Status.StaminaLeft = m_AbilityConfig.MaxStamina;
            // And broadcast current stamina
            EventManager.Instance.Broadcast(new StaminaUpdateEvent(Status.StaminaLeft, m_AbilityConfig.MaxStamina));
            // Initialize health value
            EventManager.Instance.Broadcast(new HealthUpdateEvent(20F, true));

            // Register gamemode events for updating gamemode
            gameModeCallback = e => SetGameMode(e.GameMode);
            EventManager.Instance.Register(gameModeCallback);

            // Register health update events for resetting hurt time
            healthCallback = e =>
            {
                if (m_PlayerRender.Type.MetaSlotByName.TryGetValue("data_health_id", out var metaSlot1))
                {
                    m_PlayerRender.UpdateMetadata(new Dictionary<int, object>
                    {
                        [metaSlot1] = e.Health
                    });
                }
            };
            EventManager.Instance.Register(healthCallback);
        }

        private void FixedUpdate()
        {
            var chunkRenderManager = CornApp.CurrentClient?.ChunkRenderManager;
            if (!chunkRenderManager) return;
            
            var terrainAABBs = chunkRenderManager.GetTerrainAABBs();
            var liquidAABBs = chunkRenderManager.GetLiquidAABBs();

            var wasSprinting = Status.Sprinting;
            var wasSneaking  = Status.Sneaking;
            
            BeforeCharacterUpdate(terrainAABBs, liquidAABBs);
            
            // Update player position
            CharacterUpdate(Time.fixedDeltaTime, terrainAABBs);
            
            AfterCharacterUpdate();

            // Check if sprinting status has changed
            if (wasSprinting != Status.Sprinting)
            {
                OnPlayerAction?.Invoke(wasSprinting ? EntityActionType.StopSprinting : EntityActionType.StartSprinting);
                
                // Update visual sprinting status(client prediction). The server will send it too but that would be too late.
                
            }
            
            // Check if sneaking status has changed
            if (wasSneaking != Status.Sneaking)
            {
                OnPlayerAction?.Invoke(wasSneaking ? EntityActionType.StopSneaking : EntityActionType.StartSneaking);
                
                // Update visual sprinting status(client prediction). The server will send it too but that would be too late.
                if (m_PlayerRender)
                {
                    m_PlayerRender.UpdateLocalSneakingStatus(Status.Sneaking);
                }
            }
        }

        private void OnDestroy()
        {
            Actions?.Disable();

            if (gameModeCallback is not null)
                EventManager.Instance.Unregister(gameModeCallback);
            
            if (healthCallback is not null)
                EventManager.Instance.Unregister(healthCallback);
        }

        private void SetGameMode(GameMode gameMode)
        {
            Status.GameMode = gameMode;

            switch (gameMode)
            {
                case GameMode.Survival:
                case GameMode.Creative:
                case GameMode.Adventure:
                    Status.Flying = gameMode == GameMode.Creative;
                    var initState = Status.Flying ? PlayerStates.AIRBORNE : PlayerStates.GROUNDED;
                    Status.Spectating = false;

                    if (CurrentState != initState) // Update initial player state
                    {
                        ChangeToState(initState);
                    }

                    // Update components state...
                    Status.GravityScale = 1F;

                    Status.EntityDisabled = false;

                    // Show entity render
                    if (m_PlayerRender)
                    {
                        m_PlayerRender.gameObject.SetActive(true);
                    }
                    break;
                case GameMode.Spectator:
                    Status.Spectating = true;
                    
                    if (CurrentState != PlayerStates.SPECTATE) // Update player state
                    {
                        ChangeToState(PlayerStates.SPECTATE);
                    }

                    // Update components state...
                    Status.GravityScale = 0F;

                    // Reset current velocity to zero
                    currentVelocity = Vector3.zero;

                    // Reset player status
                    Status.Grounded = false;
                    Status.InLiquid = false;
                    Status.Clinging = false;
                    Status.Sprinting = false;

                    Status.EntityDisabled = true;

                    // Hide entity render
                    if (m_PlayerRender)
                    {
                        m_PlayerRender.gameObject.SetActive(false);
                    }
                    break;
            }
        }

        public void DisablePhysics()
        {
            Status.PhysicsDisabled = true;
        }

        public void EnablePhysics()
        {
            Status.PhysicsDisabled = false;
        }
        
        public void ToggleSneaking()
        {
            Status.Sneaking = !Status.Sneaking;
        }

        public void ChangeToState(IPlayerState state)
        {
            var prevState = CurrentState;

            //Debug.Log($"Exit state [{CurrentState}]");
            CurrentState.OnExit(state, m_StatusUpdater.Status, this);

            // Exit previous state and enter this state
            CurrentState = state;
            
            //Debug.Log($"Enter state [{CurrentState}]");
            CurrentState.OnEnter(prevState, m_StatusUpdater.Status, this);
        }

        public void UseAimingCamera(bool enable)
        {
            if (m_CameraController)
            {
                // Align target visual yaw with camera, immediately
                if (enable) Status.TargetVisualYaw = m_CameraController.GetYaw();

                m_CameraController.UseAimingCamera(enable);
            }
        }

        public bool IsUsingAimingCamera()
        {
            return m_CameraController && m_CameraController.IsAimingOrLocked;
        }

        public void ToggleAimingLock()
        {
            if (m_CameraController)
            {
                // Align target visual yaw with camera, immediately
                if (!m_CameraController.AimingLocked) Status.TargetVisualYaw = m_CameraController.GetYaw();

                m_CameraController.UseAimingLock(!m_CameraController.AimingLocked);
            }
        }

        public Quaternion GetMovementOrientation()
        {
            var forward = Quaternion.AngleAxis(Status.MovementInputYaw, Vector3.up) * Vector3.forward;

            return Quaternion.LookRotation(forward, Vector3.up);
        }

        public void BeforeCharacterUpdate(UnityAABB[] terrainAABBs, UnityAABB[] liquidAABBs)
        {
            var status = m_StatusUpdater.Status;

            // Update target player visual yaw before updating player status
            var horInput = Actions!.Locomotion.Movement.ReadValue<Vector2>();
            if (horInput != Vector2.zero)
            {
                var userInputYaw = GetYawFromVector2(horInput);
                status.TargetVisualYaw = m_CameraController.GetYaw() + userInputYaw;
                status.MovementInputYaw = status.TargetVisualYaw;
            }

            // Update target visual yaw if aiming
            if (m_CameraController && m_CameraController.IsAimingOrLocked)
            {
                status.TargetVisualYaw = m_CameraController.GetYaw();

                // Align player orientation with camera view (which is set as the target value)
                status.CurrentVisualYaw = status.TargetVisualYaw;
            }

            // Update player status (in water, grounded, etc.)
            if (!status.EntityDisabled)
            {
                var dimensions = m_PlayerRender ? m_PlayerRender.GetDimensions() : Vector2.one;
                m_StatusUpdater.UpdatePlayerStatus(terrainAABBs, liquidAABBs, dimensions.x, dimensions.y);
            }

            // Update current player state
            if (pendingState != null) // Change to pending state if present
            {
                ChangeToState(pendingState);
                pendingState = null;
            }
            else if (CurrentState.ShouldExit(Actions!, status))
            {
                // Try to exit current state and enter another one
                foreach (var state in PlayerStates.STATES.Where(
                             state => state != CurrentState && state.ShouldEnter(Actions!, status)))
                {
                    ChangeToState(state);
                    break;
                }
            }
        }

        public void CharacterUpdate(float deltaTime, UnityAABB[] aabbs)
        {
            var status = m_StatusUpdater.Status;
            var prevStamina = status.StaminaLeft;
            
            // Update player physics and transform using updated current state
            CurrentState.UpdateMain(ref currentVelocity, deltaTime, Actions!, status, this);

            if (status.PhysicsDisabled)
            {
                currentVelocity = Vector3.zero;
            }
            else if (Status.GameMode == GameMode.Spectator)
            {
                // Update player position using calculated velocity, no collision, no gravity
                transform.position += currentVelocity * deltaTime;
            }
            else
            {
                var dimensions = m_PlayerRender ? m_PlayerRender.GetDimensions() : Vector2.one;
                // Update player position using calculated velocity
                m_StatusUpdater.UpdatePlayerPosition(ref currentVelocity, deltaTime, Status.Sneaking, aabbs, dimensions.x, dimensions.y);
            }

            if (!Mathf.Approximately(prevStamina, status.StaminaLeft)) // Broadcast current stamina if changed
            {
                EventManager.Instance.Broadcast(new StaminaUpdateEvent(status.StaminaLeft, m_AbilityConfig.MaxStamina));
            }
        }

        public void AfterCharacterUpdate()
        {
            var newLocation = CoordConvert.Unity2MC(_worldOriginOffset, transform.position);
            var newYaw = Status.CurrentVisualYaw;

            // Update values to send to server
            Location2Send = newLocation;
            MCYaw2Send = newYaw - 90F; // Coordinate system conversion
            Pitch2Send = 0F;
            IsGrounded2Send = Status.Grounded;

            if (m_EntityRenderManager)
            {
                const int entityId = BaseCornClient.CLIENT_ENTITY_ID_INTERNAL;
                
                // Update client player data
                m_EntityRenderManager.TeleportEntityRender(entityId, newLocation);
                m_EntityRenderManager.RotateEntityRender(entityId, newYaw, 0F);
                m_EntityRenderManager.RotateEntityRenderHead(entityId, newYaw);
            }
        }

        private Vector3Int _worldOriginOffset = Vector3Int.zero;

        /// <summary>
        /// Called when updating world origin offset to teleport the
        /// player seamlessly, returns actual position delta applied
        /// </summary>
        public Vector3 SetWorldOriginOffset(Vector3Int offset)
        {
            _worldOriginOffset = offset;

            // Recalculate position in Unity scene based on new world origin
            var updatedPosition = CoordConvert.MC2Unity(offset, Location2Send);
            var playerPosDelta = updatedPosition - transform.position;

            transform.position = updatedPosition;

            return playerPosDelta;
        }

        public void SetLocationFromServer(Location loc, bool reset = false, float mcYaw = 0F)
        {
            if (reset) // Reset current velocity
            {
                currentVelocity = Vector3.zero;
            }

            var newUnityYaw = mcYaw + 90F; // Coordinate system conversion

            Status.TargetVisualYaw = newUnityYaw;
            Status.CurrentVisualYaw = newUnityYaw;

            // Update current location and yaw
            transform.position = CoordConvert.MC2Unity(_worldOriginOffset, loc);

            if (m_EntityRenderManager)
            {
                const int entityId = BaseCornClient.CLIENT_ENTITY_ID_INTERNAL;
                
                m_EntityRenderManager.TeleportEntityRender(entityId, loc);
                m_EntityRenderManager.RotateEntityRender(entityId, newUnityYaw, 0F);
            }

            // Update local data
            Location2Send = loc;
            MCYaw2Send = mcYaw;
        }

        private static float GetYawFromVector2(Vector2 direction)
        {
            if (direction.y > 0F)
                return Mathf.Atan(direction.x / direction.y) * Mathf.Rad2Deg;
            if (direction.y < 0F)
                return Mathf.Atan(direction.x / direction.y) * Mathf.Rad2Deg + 180F;
            return direction.x > 0 ? 90F : 270F;
        }

        public string GetDebugInfo()
        {
            var statusInfo = Status.Spectating ? string.Empty : Status.ToString();

            return $"State: {CurrentState}\n{statusInfo}\nVelocity: {currentVelocity.x:0.00}\t{currentVelocity.y:0.00}\t{currentVelocity.z:0.00}";
        }
    }
}
