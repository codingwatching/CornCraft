using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using CraftSharp.Protocol.Message;

namespace CraftSharp.Rendering
{
    public class EntityRender : MonoBehaviour
    {
        protected const float MOVE_THRESHOLD = 5F * 5F; // Treat as teleport if move more than 5 meters at once

        /// <summary>
        /// ID of the entity on the Minecraft server
        /// </summary>
        public int NumeralId;

        /// <summary>
        /// UUID of the entity if it is a player.
        /// </summary>
        public Guid UUID;
        
        #nullable enable

        /// <summary>
        /// Nickname of the entity if it is a player.
        /// </summary>
        public string? Name;
        
        /// <summary>
        /// CustomName of the entity.
        /// </summary>
        public string? CustomNameJson;
        
        /// <summary>
        /// IsCustomNameVisible of the entity.
        /// </summary>
        public bool IsCustomNameVisible;

        /// <summary>
        /// CustomName of the entity.
        /// </summary>
        public string? CustomName;

        /// <summary>
        /// Entity type
        /// </summary>
        public EntityType Type { get; private set; } = EntityType.DUMMY_ENTITY_TYPE;

        /// <summary>
        /// Entity position
        /// </summary>
        public readonly TrackedValue<Vector3> Position = new(Vector3.zero);
        protected Vector3 lastTickPosition = Vector3.zero;

        protected double currentTickStartMilSec = 0F;

        /// <summary>
        /// Entity velocity received from server
        /// </summary>
        private Vector3? receivedVelocity = null;

        /// <summary>
        /// Entity yaw
        /// </summary>
        public readonly TrackedValue<float> Yaw = new(0F);
        protected float lastYaw = 0F;

        /// <summary>
        /// Entity head pitch
        /// </summary>
        public readonly TrackedValue<float> Pitch = new(0F);
        protected float lastPitch = 0F;

        /// <summary>
        /// Entity head yaw
        /// </summary>
        public readonly TrackedValue<float> HeadYaw = new(0F);
        protected float lastHeadYaw = 0F;
        
        /// <summary>
        /// Used in Item Frame, Falling Block and Fishing Float.
        /// See https://wiki.vg/Object_Data for details.
        /// </summary>
        /// <remarks>Untested</remarks>
        public int ObjectData = -1;

        /// <summary>
        /// Health of the entity
        /// </summary>
        public readonly TrackedValue<float> Health = new(0F);

        /// <summary>
        /// Max health of the entity
        /// </summary>
        public readonly TrackedValue<float> MaxHealth = new(0F);
        
        /// <summary>
        /// Item of the entity if ItemFrame or Item
        /// </summary>
        public readonly TrackedValue<ItemStack?> Item = new(null);
        
        /// <summary>
        /// Entity pose in the Minecraft world
        /// </summary>
        public EntityPose Pose;
        
        /// <summary>
        /// Entity metadata
        /// </summary>
        protected Dictionary<int, object?>? Metadata { get; private set; }

        /// <summary>
        /// Update entity metadata, validate control variables,
        /// then check for visual/material update
        /// </summary>
        public virtual void UpdateMetadata(Dictionary<int, object?> updatedMeta)
        {
            // Create if not present
            Metadata ??= new();

            foreach (var entry in updatedMeta)
            {
                Metadata[entry.Key] = entry.Value;
            }

            // Update entity health
            if (Type.MetaSlotByName.TryGetValue("data_health_id", out var metaSlot1)
                && Type.MetaEntries[metaSlot1].DataType == EntityMetadataType.Float)
            {
                if (Metadata.TryGetValue(metaSlot1, out var value) && value is float health)
                {
                    Health.Value = health;
                    MaxHealth.Value = Mathf.Max(MaxHealth.Value, health);
                }
            }

            // Update entity custom name
            if (Type.MetaSlotByName.TryGetValue("data_custom_name", out var metaSlot2)
                && Type.MetaEntries[metaSlot2].DataType == EntityMetadataType.OptionalChat)
            {
                if (Metadata.TryGetValue(metaSlot2, out var value) && value is string customName)
                {
                    CustomNameJson = customName;
                    CustomName = ChatParser.ParseText(customName);
                }
            }

            // Update entity custom name
            if (Type.MetaSlotByName.TryGetValue("data_custom_name_visible", out var metaSlot3)
                && Type.MetaEntries[metaSlot3].DataType == EntityMetadataType.Boolean)
            {
                if (Metadata.TryGetValue(metaSlot3, out var value) && value is bool customNameVisible)
                {
                    IsCustomNameVisible = customNameVisible;
                }
            }

            // Update entity pose
            if (Type.MetaSlotByName.TryGetValue("data_pose", out var metaSlot4)
                && Type.MetaEntries[metaSlot4].DataType == EntityMetadataType.Pose)
            {
                if (Metadata.TryGetValue(metaSlot4, out var value) && value is int pose)
                {
                    Pose = (EntityPose) pose;
                }
            }

            // TODO: Update control variables

            // Update own materials
            if (TryGetComponent(out EntityMaterialAssigner materialControl))
            {
                materialControl.UpdateMaterials(Type, null, updatedMeta.Keys.ToHashSet(), GetControlVariables(), Metadata);
            }
        }

        /// <summary>
        /// Entity equipment
        /// </summary>
        public Dictionary<int, ItemStack>? Equipment;

        /// <summary>
        /// Used to control animation transition (for animators) or limb swing (for vanilla entity renders)
        /// </summary>
        protected Vector3 _visualMovementVelocity = Vector3.zero;

        #nullable disable

        /// <summary>
        /// Whether the current entity has turned into ragdoll form
        /// </summary>
        protected bool _turnedIntoRagdoll = false;

        [SerializeField] protected Transform _infoAnchor;
        [SerializeField] protected Transform _visualTransform;

        /// <summary>
        /// Anchor transform for locating floating labels, health bars, etc.
        /// </summary>
        public Transform InfoAnchor
        {
            get => _infoAnchor ? _infoAnchor : transform;
            protected set => _infoAnchor = value;
        }

        /// <summary>
        /// Visual transform of the entity render
        /// </summary>
        public Transform VisualTransform
        {
            get => _visualTransform ? _visualTransform : transform;
            protected set => _visualTransform = value;
        }

        [SerializeField] protected GameObject ragdollPrefab;
        public GameObject FloatingInfoPrefab;

        /// <summary>
        /// A number made from the entity's numeral id, used in animations to prevent
        /// several mobs of a same type moving synchronisedly, which looks unnatural
        /// </summary>
        protected float _pseudoRandomOffset = 0F;

        /// <summary>
        /// Initialize this entity render
        /// </summary>
        public virtual void Initialize(EntityData source, Vector3Int originOffset)
        {
            if (!_visualTransform)
            {
                Debug.LogWarning("Visual transform for entity render not assigned!");
                _visualTransform = transform;
            }

            NumeralId = source.Id;
            UUID = source.UUID;
            Name = source.Name;
            Type = source.Type;

            Position.Value = CoordConvert.MC2Unity(originOffset, source.Location);
            lastTickPosition = Position.Value;
            lastYaw = Yaw.Value = source.Yaw;
            lastHeadYaw = HeadYaw.Value = source.HeadYaw;
            lastPitch = Pitch.Value = source.Pitch;

            Position.OnValueUpdate += (prevVal, newVal) =>
            {
                lastTickPosition = prevVal;

                // Reset lerp variable every time we receive a new position
                currentTickStartMilSec = Time.realtimeSinceStartupAsDouble * 1000D;
            };

            ObjectData = source.ObjectData;
            Health.Value = source.Health;
            MaxHealth.Value = source.MaxHealth;

            // Initialize other fields with default values
            // These fields will be later assigned by metadata packets
            CustomName = null;
            CustomNameJson = null;
            IsCustomNameVisible = true;
            Item.Value = null;
            Pose = EntityPose.Standing;
            Metadata = null;
            Equipment = null;

            UnityEngine.Random.InitState(NumeralId);
            _pseudoRandomOffset = UnityEngine.Random.Range(0F, 1F);

            _visualTransform.eulerAngles = new(0F, lastYaw, 0F);

            // Initialize materials (This requires metadata to be present)
            if (TryGetComponent(out EntityMaterialAssigner materialControl))
            {
                materialControl.InitializeMaterials(source.Type, GetControlVariables(), source.Metadata, HandleMaterialUpdate);
            }
        }
        
        public string GetDisplayName()
        {
            if (IsCustomNameVisible)
            {
                if (CustomNameJson is not null)
                {
                    return ChatParser.ParseText(CustomNameJson);
                }
                
                if (CustomName is not null)
                {
                    return CustomName;
                }
            }

            return Name ?? ChatParser.TranslateString(Type.TypeId.GetTranslationKey("entity"));
        }
        
        #nullable enable

        /// <summary>
        /// Get variable table for render control (pose, texture, etc.)
        /// </summary>
        protected virtual Dictionary<string, string>? GetControlVariables()
        {
            return null;
        }
        
        #nullable disable

        /// <summary>
        /// Finalize this entity render
        /// </summary>
        public virtual void Unload()
        {
            if (this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Used when updating world origin offset to seamlessly teleport the EntityRender
        /// and maintain its relative position to everything else
        /// </summary>
        public virtual void TeleportByDelta(Vector3 posDelta)
        {
            Position.Value += posDelta;
            transform.position += posDelta;
        }

        /// <summary>
        /// Set entity velocity from server
        /// </summary>
        public virtual void SetReceivedVelocity(Vector3 velocity)
        {
            receivedVelocity = velocity;
        }

        protected virtual void UpdateTransform(float tickMilSec)
        {
            //var currentTickMilSec = Time.realtimeSinceStartupAsDouble * 1000D - currentTickStartMilSec;

            // Update position
            if ((Position.Value - transform.position).sqrMagnitude > MOVE_THRESHOLD) // Treat as teleport
            {
                transform.position = Position.Value;
            }
            else // Smoothly move to current position
            {
                transform.position = Vector3.SmoothDamp(transform.position, Position.Value,
                        ref _visualMovementVelocity, tickMilSec / 1000F);

                /*
                if (currentTickMilSec >= tickMilSec)
                {
                    transform.position = Position.Value;
                    //lastTickPosition = Position.Value;
                }
                else
                {
                    transform.position = Vector3.Lerp(lastTickPosition, Position.Value, (float) currentTickMilSec / tickMilSec);
                }
                */

                //gameObject.name = $"XXX {(float) currentTickMilSec / tickMilSec:0.000} ({currentTickMilSec:0.00} / {tickMilSec:0.00})";
            }

            // Update rotation
            var headYawDelta = Mathf.Abs(Mathf.DeltaAngle(lastHeadYaw, HeadYaw.Value));
            var bodyYawDelta = Mathf.Abs(Mathf.DeltaAngle(lastYaw, Yaw.Value));

            if (bodyYawDelta > 0.0025F)
            {
                lastYaw = Mathf.MoveTowardsAngle(lastYaw, Yaw.Value, Time.deltaTime * 300F);
                _visualTransform!.eulerAngles = new(0F, lastYaw, 0F);
            }

            if (headYawDelta > 0.0025F)
            {
                lastHeadYaw = Mathf.MoveTowardsAngle(lastHeadYaw, HeadYaw.Value, Time.deltaTime * 150F);
            }
            else
            {
                if (_visualMovementVelocity.magnitude < 0.1F)
                {
                    Yaw.Value = HeadYaw.Value;
                }
            }
            
            if (Mathf.Abs(Mathf.DeltaAngle(Yaw.Value, HeadYaw.Value)) > 75F)
            {
                Yaw.Value = HeadYaw.Value;
            }
            
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (lastPitch != Pitch.Value)
            {
                lastPitch = Mathf.MoveTowardsAngle(lastPitch, Pitch.Value, Time.deltaTime * 300F);
            }
        }

        public virtual Transform SetupCameraRef()
        {
            var aimingLocalHeight = transform.InverseTransformPoint(GetAimingRef().position).y;
            var pos = new Vector3(0F, aimingLocalHeight, 0F);

            return SetupCameraRef(pos);
        }
        
        public virtual Transform GetAimingRef()
        {
            return transform;
        }

        protected Transform SetupCameraRef(Vector3 pos)
        {
            var cameraRefObj = new GameObject("Camera Ref Obj");
            var cameraRef = cameraRefObj.transform;

            cameraRef.SetParent(_visualTransform, false);
            cameraRef.localPosition = pos;

            return cameraRef;
        }

        public virtual void SetVisualMovementVelocity(Vector3 velocity, Vector3 upDirection)
        {
            _visualMovementVelocity = velocity;
        }

        public virtual void UpdateAnimation(float tickMilSec) { }

        protected virtual void HandleMaterialUpdate(EntityMaterialManager matManager, ResourceLocation textureId, Material updatedMaterial)
        {

        }

        protected virtual void HandleRagdollMaterialUpdate(EntityMaterialAssigner ragdollMaterialControl, EntityMaterialManager matManager, ResourceLocation textureId, Material updatedMaterial)
        {

        }

        protected virtual void TurnIntoRagdoll()
        {
            _turnedIntoRagdoll = true;

            // Create ragdoll in place
            if (ragdollPrefab)
            {
                var ragdollObj = GameObject.Instantiate(ragdollPrefab)!;

                ragdollObj.transform.position = transform.position;

                // Assign own rotation and localScale to ragdoll object
                var ragdoll = ragdollObj.GetComponentInChildren<EntityRagdoll>();
                if (ragdoll && _visualTransform)
                {
                    ragdollObj.transform.rotation = _visualTransform.rotation;
                    ragdoll.Visual.localScale = _visualTransform.localScale;

                    // Make it fly!
                    if (ragdoll.mainRigidbody)
                    {
                        if (_visualMovementVelocity.sqrMagnitude > 0F)
                            ragdoll.mainRigidbody.AddForce(_visualMovementVelocity.normalized * 15F, ForceMode.VelocityChange);
                        else
                            ragdoll.mainRigidbody.AddForce(Vector3.up * 10F, ForceMode.VelocityChange);
                    }

                    // Initialize ragdoll materials using own metadata
                    if (ragdoll.gameObject.TryGetComponent(out EntityMaterialAssigner materialControl))
                    {
                        materialControl.InitializeMaterials(Type, GetControlVariables(), Metadata, (matManager, texId, mat) =>
                        {
                            HandleRagdollMaterialUpdate(materialControl, matManager, texId, mat);
                        });
                    }
                }

                // Hide own visual
                if (_visualTransform)
                {
                    _visualTransform.gameObject.SetActive(false);
                }
            }
        }

        public virtual void ManagedUpdate(float tickMilSec)
        {
            if (!_turnedIntoRagdoll && Health.Value <= 0F)
            {
                TurnIntoRagdoll();
            }

            UpdateTransform(tickMilSec);
            UpdateAnimation(tickMilSec);
        }
    }
}