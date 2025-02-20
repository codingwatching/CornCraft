#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using CraftSharp.Event;
using CraftSharp.Rendering;

namespace CraftSharp.Control
{
    public class InteractionId
    {
        private readonly BitArray usage = new(int.MaxValue);
        private int currentId = 0;

        public int AllocateID()
        {
            while (currentId < usage.Length)
            {
                if (!usage[currentId])
                {
                    usage[currentId] = true;
                    return currentId++;
                }

                currentId++;
            }

            return -1;
        }

        public void ReleaseID(int id)
        {
            if (id >= 0 && id < usage.Length)
                usage[id] = false;
        }
    }

    public class InteractionUpdater : MonoBehaviour
    {
        public const int MAX_TARGET_DISTANCE = 8;
        public const int BLOCK_INTERACTION_RADIUS = 3;
        public const float BLOCK_INTERACTION_RADIUS_SQR = 9.0f; // BLOCK_INTERACTION_RADIUS ^ 2
        public const float BLOCK_INTERACTION_RADIUS_SQR_PLUS = 12.25f; // (BLOCK_INTERACTION_RADIUS + 0.5f) ^ 2
        
        private static readonly List<BlockLoc> validOffsets = ComputeOffsets();

        private static List<BlockLoc> ComputeOffsets()
        {
            var offsets = new List<BlockLoc>();
            for (int x = -BLOCK_INTERACTION_RADIUS; x <= BLOCK_INTERACTION_RADIUS; x++)
                for (int y = -BLOCK_INTERACTION_RADIUS; y <= BLOCK_INTERACTION_RADIUS; y++)
                    for (int z = -BLOCK_INTERACTION_RADIUS; z <= BLOCK_INTERACTION_RADIUS; z++)
                        if (x * x + y * y + z * z <= BLOCK_INTERACTION_RADIUS_SQR)
                            offsets.Add(new BlockLoc(x, y, z));
            return offsets;
        }

        [SerializeField] private LayerMask blockSelectionLayer;
        [SerializeField] private GameObject? blockSelectionFramePrefab;

        private BlockSelectionBox? blockSelectionBox;

        private BaseCornClient? client;
        private CameraController? cameraController;
        private PlayerController? playerController;

        private Action<HeldItemChangeEvent>? heldItemCallback;
        private Action<TriggerInteractionExecutionEvent>? triggerInteractionExecutionEvent;
        private Action<HarvestInteractionUpdateEvent>? harvestInteractionUpdateCallback;

        private readonly Dictionary<int, InteractionInfo> interactionInfos = new();
        private readonly Dictionary<BlockLoc, List<BlockTriggerInteractionInfo>> blockTriggerInteractionInfos = new();

        private readonly InteractionId interactionId = new();

        private LocalHarvestInteractionInfo? lastHarvestInteractionInfo;
        private Item? currentItem;

        public Direction? TargetDirection { get; private set; } = Direction.Down;
        public BlockLoc? TargetBlockLoc { get; private set; } = null;

        private void UpdateBlockSelection(Ray? viewRay)
        {
            if (viewRay is null || client == null) return;

            if (Physics.Raycast(viewRay.Value.origin, viewRay.Value.direction, out RaycastHit viewHit, MAX_TARGET_DISTANCE, blockSelectionLayer))
            {
                Vector3 normal = viewHit.normal.normalized;
                TargetDirection = GetDirectionFromNormal(normal);

                Vector3 offseted = PointOnCubeSurface(viewHit.point)
                    ? viewHit.point - normal * 0.5f
                    : viewHit.point;

                Vector3 unityBlockPos = new(
                    Mathf.FloorToInt(offseted.x),
                    Mathf.FloorToInt(offseted.y),
                    Mathf.FloorToInt(offseted.z)
                );

                var newTargetLoc = CoordConvert.Unity2MC(client.WorldOriginOffset, unityBlockPos).GetBlockLoc();
                var block = client.ChunkRenderManager.GetBlock(newTargetLoc);

                // Create selection box if not present
                if (blockSelectionBox == null)
                {
                    blockSelectionBox = Instantiate(blockSelectionFramePrefab)!.GetComponent<BlockSelectionBox>();
                    blockSelectionBox!.transform.SetParent(transform, false);
                }

                // Update target location if changed
                if (TargetBlockLoc != newTargetLoc)
                {
                    TargetBlockLoc = newTargetLoc;
                    blockSelectionBox.transform.position = unityBlockPos;

                    EventManager.Instance.Broadcast(new TargetBlockLocChangeEvent(newTargetLoc));
                }

                // Update shape even if target location is not changed (the block itself may change)
                blockSelectionBox.UpdateShape(block.State.Shape);
            }
            else
            {
                // Update target location if changed
                if (TargetBlockLoc != null)
                {
                    TargetBlockLoc = null;

                    EventManager.Instance.Broadcast(new TargetBlockLocChangeEvent(null));
                }

                // Clear shape if selection box is created
                if (blockSelectionBox != null)
                {
                    blockSelectionBox.ClearShape();
                }
            }
            
            static Direction GetDirectionFromNormal(Vector3 normal)
            {
                float absX = Mathf.Abs(normal.x);
                float absY = Mathf.Abs(normal.y);
                float absZ = Mathf.Abs(normal.z);

                if (absX >= absY && absX >= absZ)
                    return normal.x > 0 ? Direction.East : Direction.West;
                if (absY >= absX && absY >= absZ)
                    return normal.y > 0 ? Direction.Up : Direction.Down;

                return normal.z > 0 ? Direction.North : Direction.South;
            }

            static bool PointOnCubeSurface(Vector3 point)
            {
                Vector3 delta = new(
                    point.x - Mathf.Floor(point.x),
                    point.y - Mathf.Floor(point.y),
                    point.z - Mathf.Floor(point.z)
                );

                return delta.x is < 0.01f or > 0.99f ||
                       delta.y is < 0.01f or > 0.99f ||
                       delta.z is < 0.01f or > 0.99f;
            }
        }

        private void UpdateBlockInteractions(ChunkRenderManager chunksManager)
        {
            var playerBlockLoc = client!.GetCurrentLocation().GetBlockLoc();
            var table = InteractionManager.INSTANCE.InteractionTable;

            if (client == null) return;

            foreach (var blockLoc in blockTriggerInteractionInfos.Keys.ToList()) // ToList because collection may change
            {
                // Remove trigger interactions which are too far from player
                if (playerBlockLoc.SqrDistanceTo(blockLoc) > BLOCK_INTERACTION_RADIUS_SQR_PLUS)
                {
                    if (blockLoc != TargetBlockLoc) // Make an exception for target location
                    {
                        RemoveBlockTriggerInteractionsAt(blockLoc, info =>
                        {
                            EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(info.Id));
                        });
                        //Debug.Log($"Rem: [{blockLoc}]");
                    }
                }
            }

            // Update harvest interactions (these are not bound by interaction radius)
            if (lastHarvestInteractionInfo != null)
            {
                if (lastHarvestInteractionInfo.Location != TargetBlockLoc || !lastHarvestInteractionInfo.UpdateInteraction(client))
                {
                    RemoveInteraction<HarvestInteractionInfo>(lastHarvestInteractionInfo.Id, info =>
                    {
                        EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(info.Id));
                    });

                    lastHarvestInteractionInfo = null;
                }
            }

            var availableBlockLocs = validOffsets.Select(offset => offset + playerBlockLoc);
            if (TargetBlockLoc != null)
            {
                availableBlockLocs = availableBlockLocs.Append(TargetBlockLoc.Value);
            }

            // Append new available trigger interactions
            foreach (var blockLoc in availableBlockLocs)
            {
                var block = chunksManager.GetBlock(blockLoc);

                if (table.TryGetValue(block.StateId, out InteractionDefinition? newInteractionDefinition))
                {
                    var newTriggerInteraction = newInteractionDefinition?.Get<TriggerInteraction>();
                    if (newTriggerInteraction is null) continue;

                    var prevInfo = GetBlockTriggerInteractionsAt(blockLoc)?.FirstOrDefault();
                    var newInfo = new BlockTriggerInteractionInfo(interactionId.AllocateID(), block, blockLoc, block.BlockId, newTriggerInteraction);

                    if (prevInfo is not null)
                    {
                        var prevDefinition = prevInfo.Definition;
                        if (prevDefinition != newTriggerInteraction) // Update this interaction
                        {
                            RemoveBlockTriggerInteractionsAt(blockLoc, info =>
                            {
                                EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(info.Id));
                            });
                            AddBlockTriggerInteractionAt(blockLoc, newInfo, info =>
                            {
                                // Select this new item if it is at target location
                                EventManager.Instance.Broadcast<InteractionAddEvent>(new(info.Id, TargetBlockLoc == blockLoc, false, info));
                            });
                            //Debug.Log($"Upd: [{blockLoc}] {prevDefinition.Identifier} => {newDefinition.Identifier}");
                        }
                        // Otherwise leave it unchanged
                    }
                    else // Add this interaction
                    {
                        AddBlockTriggerInteractionAt(blockLoc, newInfo, info =>
                        {
                            // Select this new item if it is at target location
                            EventManager.Instance.Broadcast<InteractionAddEvent>(new(info.Id, TargetBlockLoc == blockLoc, false, info));
                        });
                        //Debug.Log($"Add: [{blockLoc}] {newDefinition.Identifier}");
                    }
                }
                else
                {
                    if (blockTriggerInteractionInfos.ContainsKey(blockLoc))
                    {
                        RemoveBlockTriggerInteractionsAt(blockLoc, info =>
                        {
                            EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(info.Id));
                        });
                        //Debug.Log($"Rem: [{blockLoc}]");
                    }
                }
            }
        }

        private void AddInteraction<T>(int id, T info, Action<T>? onCreated = null) where T : InteractionInfo
        {
            interactionInfos.Add(id, info);

            onCreated?.Invoke(info);
        }

        private void AddBlockTriggerInteractionAt(BlockLoc location, BlockTriggerInteractionInfo info, Action<BlockTriggerInteractionInfo>? onCreated = null)
        {
            if (!blockTriggerInteractionInfos.TryGetValue(location, out var infosAtLoc))
            {
                infosAtLoc = new List<BlockTriggerInteractionInfo>();
                blockTriggerInteractionInfos[location] = infosAtLoc;
            }

            infosAtLoc.Add(info);

            AddInteraction(info.Id, info, onCreated);
        }

        private T? GetInteraction<T>(int id) where T : InteractionInfo
        {
            return interactionInfos.TryGetValue(id, out var interactionInfo) ? (T) interactionInfo : null;
        }

        private IEnumerable<BlockTriggerInteractionInfo>? GetBlockTriggerInteractionsAt(BlockLoc blockLoc)
        {
            return blockTriggerInteractionInfos.TryGetValue(blockLoc, out var infos) ? infos : null;
        }

        private void RemoveInteraction<T>(int id, Action<T>? onRemoved = null) where T : InteractionInfo
        {
            if (interactionInfos.Remove(id, out var removedInfo))
            {
                interactionId.ReleaseID(id);
                onRemoved?.Invoke((T) removedInfo);
            }
        }

        private void RemoveBlockTriggerInteractionAt(BlockLoc blockLoc, int id, Action<BlockTriggerInteractionInfo>? onRemoved = null)
        {
            if (blockTriggerInteractionInfos.TryGetValue(blockLoc, out var infosAtLoc))
            {
                infosAtLoc.RemoveAll(interactionInfo => // Only up to 1 interaction will be removed
                {
                    if (interactionInfo.Id == id)
                    {
                        RemoveInteraction(id, onRemoved);

                        return true;
                    }
                    return false;
                });

                // Remove this entry if no interaction left at this location
                if (!infosAtLoc.Any()) blockTriggerInteractionInfos.Remove(blockLoc);
            }
        }

        private void RemoveBlockTriggerInteractionsAt(BlockLoc blockLoc, Action<BlockTriggerInteractionInfo>? onEachRemoved = null)
        {
            if (blockTriggerInteractionInfos.TryGetValue(blockLoc, out var infosAtLoc))
            {
                infosAtLoc.RemoveAll(interactionInfo =>
                {
                    RemoveInteraction(interactionInfo.Id, onEachRemoved);

                    return true;
                });

                // Remove this entry
                blockTriggerInteractionInfos.Remove(blockLoc);
            }
        }

        public void Initialize(BaseCornClient client, CameraController camController, PlayerController playerController)
        {
            this.client = client;
            this.cameraController = camController;
            this.playerController = playerController;
        }

        void Start()
        {
            heldItemCallback = e => currentItem = e.ItemStack?.ItemType;
            EventManager.Instance.Register(heldItemCallback);

            harvestInteractionUpdateCallback = e =>
            {
                var harvestInteractionInfo = GetInteraction<HarvestInteractionInfo>(e.InteractionId);
                if (harvestInteractionInfo is not null)
                {
                    // Update the process
                    harvestInteractionInfo.Progress = e.Progress;
                }
            };
            EventManager.Instance.Register(harvestInteractionUpdateCallback);

            triggerInteractionExecutionEvent = e =>
            {
                var triggerInteractionInfo = GetInteraction<InteractionInfo>(e.InteractionId);

                if (triggerInteractionInfo != null && client != null)
                {
                    triggerInteractionInfo.UpdateInteraction(client);

                    // Remove it immediately after execution TODO: Maybe check the result?
                    RemoveInteraction<InteractionInfo>(e.InteractionId, info =>
                    {
                        EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(info.Id));

                        // Check block trigger interaction removal
                        if (info is BlockTriggerInteractionInfo blockInfo)
                        {
                            var blockLoc = blockInfo.BlockLoc;
                            RemoveBlockTriggerInteractionAt(blockLoc, info.Id);
                        }
                    });
                }
            };
            EventManager.Instance.Register(triggerInteractionExecutionEvent);
        }

        private void StartDiggingProcess(Block block, BlockLoc blockLoc, Direction direction, PlayerStatus status)
        {
            var definition = InteractionManager.INSTANCE.InteractionTable
                .GetValueOrDefault(block.StateId)?
                .Get<HarvestInteraction>();
            
            if (definition is null)
            {
                Debug.LogWarning($"Harvest interaction for {block.State} is not registered.");
                return;
            }
            
            if (lastHarvestInteractionInfo is not null)
            {
                lastHarvestInteractionInfo.CancelInteraction();
                EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(lastHarvestInteractionInfo.Id));
            }

            lastHarvestInteractionInfo = new LocalHarvestInteractionInfo(interactionId.AllocateID(), block, blockLoc, direction,
                currentItem, block.State.Hardness, status.Floating, status.Grounded, definition);
            
            //Debug.Log($"Created {lastHarvestInteractionInfo.GetHashCode()} at {blockLoc}");

            AddInteraction(lastHarvestInteractionInfo.Id, lastHarvestInteractionInfo, info =>
            {
                EventManager.Instance.Broadcast<InteractionAddEvent>(new(info.Id, true, true, info));
            });
        }

        void Update()
        {
            if (cameraController != null && cameraController.IsAimingOrLocked)
            {
                UpdateBlockSelection(cameraController.GetPointerRay());

                if (TargetBlockLoc is not null && TargetDirection is not null &&
                    playerController != null && client != null && playerController.CurrentState is DiggingAimState)
                {
                    var block = client.ChunkRenderManager.GetBlock(TargetBlockLoc.Value);
                    var status = playerController.Status;
                    if (lastHarvestInteractionInfo is not null)
                    {
                        if (lastHarvestInteractionInfo.State == HarvestInteractionState.Completed)
                        {
                            lastHarvestInteractionInfo = null;
                        }
                    }
                    else if (!block.State.NoSolidMesh)
                    {
                        StartDiggingProcess(block, TargetBlockLoc.Value, TargetDirection.Value, status);
                    }
                }
                else
                {
                    if (lastHarvestInteractionInfo is not null)
                    {
                        lastHarvestInteractionInfo.CancelInteraction();
                        EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(lastHarvestInteractionInfo.Id));
                        
                        lastHarvestInteractionInfo = null;
                    }
                }
            }
            else
            {
                if (lastHarvestInteractionInfo is not null)
                {
                    lastHarvestInteractionInfo.CancelInteraction();
                    EventManager.Instance.Broadcast<InteractionRemoveEvent>(new(lastHarvestInteractionInfo.Id));
                    
                    lastHarvestInteractionInfo = null;
                }
                
                TargetBlockLoc = null;

                if (blockSelectionBox != null)
                {
                    blockSelectionBox.ClearShape();
                }
            }
        }

        private void LateUpdate()
        {
            if (client != null)
            {
                // Update block interactions
                UpdateBlockInteractions(client.ChunkRenderManager);
            }
        }

        private void OnDestroy()
        {
            if (heldItemCallback is not null)
                EventManager.Instance.Unregister(heldItemCallback);
        }
    }
}