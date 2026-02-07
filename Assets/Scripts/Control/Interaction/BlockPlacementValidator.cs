using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CraftSharp.Control
{
    public sealed class BlockPlacementValidator
    {
        public static readonly ResourceLocation REPLACEABLE_TAG_ID = new("corncraft/replaceable_compat");
        private static readonly ResourceLocation DIRT_TAG_ID = new("dirt");
        private static readonly ResourceLocation PLANTS_TAG_ID = new("corncraft/plants");
        private static readonly ResourceLocation AZALEA_ID = new("azalea");
        private static readonly ResourceLocation AZALEA_GROWS_ON_TAG_ID = new("azalea_grows_on");
        private static readonly ResourceLocation BAMBOOS_TAG_ID = new("corncraft/bamboos");
        private static readonly ResourceLocation BAMBOO_PLANTABLE_ON_TAG_ID = new("bamboo_plantable_on");
        private static readonly ResourceLocation CROPS_TAG_ID = new("crops");
        private static readonly ResourceLocation FARMLAND_ID = new("farmland");
        private static readonly ResourceLocation CACTUS_ID = new("cactus");
        private static readonly ResourceLocation SAND_TAG_ID = new("sand");
        private static readonly ResourceLocation SMALL_DRIPLEAF_ID = new("small_dripleaf");
        private static readonly ResourceLocation SMALL_DRIPLEAF_PLACEABLE_TAG_ID = new("small_dripleaf_placeable");
        private static readonly ResourceLocation BIG_DRIPLEAF_ID = new("big_dripleaf");
        private static readonly ResourceLocation BIG_DRIPLEAF_PLACEABLE_TAG_ID = new("big_dripleaf_placeable");

        private readonly BaseCornClient client;
        private readonly Dictionary<ResourceLocation, Func<BlockLoc, BlockState, bool>> neighborChecks = new();

        public BlockPlacementValidator(BaseCornClient client)
        {
            this.client = client;
            RegisterStaticNeighborChecks();
            RegisterIdPathBasedNeighborChecks();
        }

        public bool IsPlacementValid(BlockLoc blockLoc, BlockState blockState)
        {
            // Check if current block at target location is replaceable
            var currentBlockState = client.ChunkRenderManager.GetBlock(blockLoc).State;
            if (!GroupTag.TryGetTag("block", REPLACEABLE_TAG_ID, out var replaceableTag) ||
                !replaceableTag.Contains(currentBlockState.BlockId))
            {
                return false; // Current block is not replaceable
            }

            if (!CheckBlockPlacementNeighbors(blockLoc, blockState))
            {
                return false;
            }

            if (blockState.NoCollision) return true;

            // Get all AABBs from the block state (use ColliderAABBs if available, otherwise AABBs)
            var blockAABBs = blockState.Shape.ColliderAABBs ?? blockState.Shape.AABBs;
            if (blockAABBs.Count == 0) return true; // No collision AABBs, placement is valid

            // Get nearby entities including the client player
            var entityRenderManager = client.EntityRenderManager;
            var nearbyEntityIds = entityRenderManager.GetNearbyEntityIds();

            // Also include the client player entity
            var clientEntityRender = entityRenderManager.GetEntityRender(BaseCornClient.CLIENT_ENTITY_ID_INTERNAL);

            var worldPositionOffset = CoordConvert.GetPosDelta(client.WorldOriginOffset);

            // Check each block AABB against all entity AABBs
            foreach (var blockAABB in blockAABBs)
            {
                // Convert block AABB to world space (Minecraft coordinates)
                // Block AABBs are in block-local coordinates (0-1 range), add block location
                var worldBlockAABB = new ShapeAABB(
                    blockAABB.MinX + blockLoc.X + worldPositionOffset.z,
                    blockAABB.MinY + blockLoc.Y + worldPositionOffset.y,
                    blockAABB.MinZ + blockLoc.Z + worldPositionOffset.x,
                    blockAABB.MaxX + blockLoc.X + worldPositionOffset.z,
                    blockAABB.MaxY + blockLoc.Y + worldPositionOffset.y,
                    blockAABB.MaxZ + blockLoc.Z + worldPositionOffset.x
                );

                // Check against client player entity
                if (clientEntityRender)
                {
                    var playerAABB = clientEntityRender.GetAABB();

                    if (AABBsOverlap(worldBlockAABB, playerAABB))
                    {
                        return false; // Overlaps with player
                    }
                }

                // Check against all nearby entities
                foreach (var entityId in nearbyEntityIds.Keys)
                {
                    var entityRender = entityRenderManager.GetEntityRender(entityId);
                    if (!entityRender || entityRender.IsClientEntity) continue; // Skip if null or already checked client entity

                    var entityAABB = entityRender.GetAABB();
                    if (AABBsOverlap(worldBlockAABB, entityAABB))
                    {
                        return false; // Overlaps with entity
                    }
                }
            }

            return true;
        }

        private bool CheckBlockPlacementNeighbors(BlockLoc blockLoc, BlockState blockState)
        {
            var blockId = blockState.BlockId;

            if (TryGetNeighborCheck(blockId, out var neighborCheck) && !neighborCheck(blockLoc, blockState))
            {
                return false;
            }

            return true;
        }

        private void RegisterStaticNeighborChecks()
        {
            neighborChecks[AZALEA_ID] = (blockLoc, _) => CheckBlockBelowTag(blockLoc, AZALEA_GROWS_ON_TAG_ID);
            neighborChecks[CACTUS_ID] = (blockLoc, _) => CheckBlockBelowTagOrBlockId(blockLoc, SAND_TAG_ID, CACTUS_ID);
            neighborChecks[SMALL_DRIPLEAF_ID] = (blockLoc, _) => CheckBlockBelowTag(blockLoc, SMALL_DRIPLEAF_PLACEABLE_TAG_ID);
            neighborChecks[BIG_DRIPLEAF_ID] = (blockLoc, _) => CheckBlockBelowTag(blockLoc, BIG_DRIPLEAF_PLACEABLE_TAG_ID);
            PopulateTagNeighborChecks(BAMBOOS_TAG_ID, (blockLoc, _) => CheckBlockBelowTag(blockLoc, BAMBOO_PLANTABLE_ON_TAG_ID));
            PopulateTagNeighborChecks(CROPS_TAG_ID, (blockLoc, _) => CheckBlockBelowBlockId(blockLoc, FARMLAND_ID));
            PopulateTagNeighborChecks(PLANTS_TAG_ID, (blockLoc, _) => CheckBlockBelowTagOrBlockId(blockLoc, DIRT_TAG_ID, FARMLAND_ID));
        }

        private void RegisterIdPathBasedNeighborChecks()
        {
            var palette = BlockStatePalette.INSTANCE;
            foreach (var blockId in palette.GetAllGroupIds())
            {
                if (neighborChecks.ContainsKey(blockId))
                {
                    continue;
                }

                if (blockId.Path.EndsWith("wall_torch"))
                {
                    neighborChecks[blockId] = (blockLoc, blockState) => CheckWallAttachSupport(blockLoc, blockState, true);
                }
                else if (blockId.Path.EndsWith("torch"))
                {
                    neighborChecks[blockId] = (blockLoc, _) => CheckGroundSupport(blockLoc, true);
                }

                if (blockId.Path.EndsWith("wall_banner"))
                {
                    neighborChecks[blockId] = (blockLoc, blockState) => CheckWallAttachSupport(blockLoc, blockState, false);
                }
                else if (blockId.Path.EndsWith("banner"))
                {
                    neighborChecks[blockId] = (blockLoc, _) => CheckGroundSupport(blockLoc, false);
                }

                if (blockId.Path.EndsWith("door"))
                {
                    neighborChecks[blockId] = (blockLoc, _) => CheckGroundSupport(blockLoc, true);
                }

                if (blockId.Path.EndsWith("carpet"))
                {
                    neighborChecks[blockId] = (blockLoc, _) => CheckGroundSupport(blockLoc, false);
                }
            }
        }

        private bool CheckWallAttachSupport(BlockLoc blockLoc, BlockState blockState, bool checkFullFace)
        {
            if (!TryGetWallAttachDirection(blockState, out var attachDirection))
            {
                return true;
            }

            var supportLoc = GetNeighborLoc(blockLoc, attachDirection);
            var supportState = client.ChunkRenderManager.GetBlock(supportLoc).State;
            var supportFace = GetOppositeDirection(attachDirection);

            return IsFaceSolid(supportState, supportFace, checkFullFace);
        }

        private bool CheckGroundSupport(BlockLoc blockLoc, bool checkFullFace)
        {
            var supportLoc = blockLoc.Down();
            var supportState = client.ChunkRenderManager.GetBlock(supportLoc).State;
            var supportFace = Direction.Up;

            return IsFaceSolid(supportState, supportFace, checkFullFace);
        }

        private bool TryGetNeighborCheck(ResourceLocation blockId, out Func<BlockLoc, BlockState, bool> check)
        {
            return neighborChecks.TryGetValue(blockId, out check);
        }

        private void PopulateTagNeighborChecks(ResourceLocation tagId, Func<BlockLoc, BlockState, bool> check)
        {
            if (!GroupTag.TryGetEntries("block", tagId, out var blockIds))
            {
                return;
            }

            foreach (var blockId in blockIds)
            {
                if (!neighborChecks.ContainsKey(blockId))
                {
                    neighborChecks[blockId] = check;
                }
            }
        }

        private bool CheckBlockBelowTag(BlockLoc blockLoc, ResourceLocation tagId)
        {
            if (!GroupTag.TryGetTag("block", tagId, out var tag))
            {
                return false;
            }

            var belowState = client.ChunkRenderManager.GetBlock(blockLoc.Down()).State;
            return tag.Contains(belowState.BlockId);
        }

        private bool CheckBlockBelowBlockId(BlockLoc blockLoc, ResourceLocation blockId)
        {
            var belowState = client.ChunkRenderManager.GetBlock(blockLoc.Down()).State;
            return belowState.BlockId == blockId;
        }

        private bool CheckBlockBelowTagOrBlockId(BlockLoc blockLoc, ResourceLocation tagId, ResourceLocation blockId)
        {
            if (!GroupTag.TryGetTag("block", tagId, out var tag))
            {
                return false;
            }

            var belowState = client.ChunkRenderManager.GetBlock(blockLoc.Down()).State;
            return tag.Contains(belowState.BlockId) || belowState.BlockId == blockId;
        }

        private static BlockLoc GetNeighborLoc(BlockLoc targetLoc, Direction targetDir)
        {
            return targetDir switch
            {
                Direction.Down => targetLoc.Down(),
                Direction.Up => targetLoc.Up(),
                Direction.South => targetLoc.South(),
                Direction.North => targetLoc.North(),
                Direction.East => targetLoc.East(),
                Direction.West => targetLoc.West(),
                _ => throw new InvalidDataException($"Invalid direction {targetDir}"),
            };
        }

        private static bool TryGetWallAttachDirection(BlockState blockState, out Direction attachDirection)
        {
            attachDirection = default;

            if (blockState.Properties.TryGetValue("face", out var faceValue) && faceValue == "wall" &&
                blockState.Properties.TryGetValue("facing", out var facingValue) &&
                TryParseDirection(facingValue, out var facingDirection))
            {
                attachDirection = GetOppositeDirection(facingDirection);
                return attachDirection is Direction.North or Direction.South or Direction.East or Direction.West;
            }

            if (blockState.BlockId.Path.EndsWith("wall_torch") &&
                blockState.Properties.TryGetValue("facing", out var torchFacing) &&
                TryParseDirection(torchFacing, out var torchFacingDirection))
            {
                attachDirection = GetOppositeDirection(torchFacingDirection);
                return true;
            }

            return false;
        }

        private static bool TryParseDirection(string value, out Direction direction)
        {
            switch (value)
            {
                case "north":
                    direction = Direction.North;
                    return true;
                case "south":
                    direction = Direction.South;
                    return true;
                case "east":
                    direction = Direction.East;
                    return true;
                case "west":
                    direction = Direction.West;
                    return true;
                case "up":
                    direction = Direction.Up;
                    return true;
                case "down":
                    direction = Direction.Down;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }

        private static Direction GetOppositeDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Down => Direction.Up,
                Direction.Up => Direction.Down,
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                _ => throw new InvalidDataException($"Invalid direction {direction}"),
            };
        }

        private static bool IsFaceSolid(BlockState blockState, Direction face, bool checkFullFace)
        {
            if (blockState.NoCollision || blockState.NoSolidMesh) return false;
            if (blockState.FullShape) return true;

            var aabbs = blockState.Shape.ColliderAABBs ?? blockState.Shape.AABBs;
            if (aabbs.Count == 0) return false;

            if (!checkFullFace)
            {
                return true;
            }

            const float epsilon = 0.0001f;

            foreach (var aabb in aabbs)
            {
                switch (face)
                {
                    case Direction.Up:
                        if (Mathf.Abs(aabb.MaxY - 1F) <= epsilon &&
                            aabb.MinX <= epsilon && aabb.MaxX >= 1F - epsilon &&
                            aabb.MinZ <= epsilon && aabb.MaxZ >= 1F - epsilon)
                            return true;
                        break;
                    case Direction.Down:
                        if (Mathf.Abs(aabb.MinY) <= epsilon &&
                            aabb.MinX <= epsilon && aabb.MaxX >= 1F - epsilon &&
                            aabb.MinZ <= epsilon && aabb.MaxZ >= 1F - epsilon)
                            return true;
                        break;
                    case Direction.North:
                        if (Mathf.Abs(aabb.MinZ) <= epsilon &&
                            aabb.MinX <= epsilon && aabb.MaxX >= 1F - epsilon &&
                            aabb.MinY <= epsilon && aabb.MaxY >= 1F - epsilon)
                            return true;
                        break;
                    case Direction.South:
                        if (Mathf.Abs(aabb.MaxZ - 1F) <= epsilon &&
                            aabb.MinX <= epsilon && aabb.MaxX >= 1F - epsilon &&
                            aabb.MinY <= epsilon && aabb.MaxY >= 1F - epsilon)
                            return true;
                        break;
                    case Direction.East:
                        if (Mathf.Abs(aabb.MaxX - 1F) <= epsilon &&
                            aabb.MinZ <= epsilon && aabb.MaxZ >= 1F - epsilon &&
                            aabb.MinY <= epsilon && aabb.MaxY >= 1F - epsilon)
                            return true;
                        break;
                    case Direction.West:
                        if (Mathf.Abs(aabb.MinX) <= epsilon &&
                            aabb.MinZ <= epsilon && aabb.MaxZ >= 1F - epsilon &&
                            aabb.MinY <= epsilon && aabb.MaxY >= 1F - epsilon)
                            return true;
                        break;
                }
            }

            return false;
        }

        private static bool AABBsOverlap(ShapeAABB a, ShapeAABB b)
        {
            // Two AABBs overlap if they overlap on all three axes
            return a.MinX < b.MaxX && a.MaxX > b.MinX &&
                   a.MinY < b.MaxY && a.MaxY > b.MinY &&
                   a.MinZ < b.MaxZ && a.MaxZ > b.MinZ;
        }
    }
}
