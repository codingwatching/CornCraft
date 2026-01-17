using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CraftSharp.Control
{
    public static class BlockPlacementPredictor
    {
        private static readonly ResourceLocation COCOA_ID = new("cocoa");
        
        private static readonly HashSet<ResourceLocation> ANVIL_IDS = new()
        {
            new("anvil"), new("chipped_anvil"), new("damaged_anvil")
        };
        
        public static (int, BlockState) PredictBlockPlacement(ResourceLocation blockId,
            Direction targetDirection, float cameraYaw, float cameraPitch, bool clickedTopHalf, bool replace)
        {
            var cameraYawDir = PlayerStatus.GetYawDirection(cameraYaw);

            // Special handling for blocks with their wall-attached with unique ids
            if (targetDirection != Direction.Up)
            {
                if (blockId.Path.EndsWith("torch"))
                {
                    blockId = new ResourceLocation(blockId.Namespace, blockId.Path.Replace("torch", "wall_torch"));
                }
                else if (blockId.Path.EndsWith("banner"))
                {
                    blockId = new ResourceLocation(blockId.Namespace, blockId.Path.Replace("banner", "wall_banner"));
                } 
            }

            var palette = BlockStatePalette.INSTANCE;
            var propTable = palette.GetBlockProperties(blockId);
            var predicateProps = palette.GetDefault(blockId).Properties
                // Make a copy of default property dictionary
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            // See https://bugs.mojang.com/browse/MC/issues/MC-193943
            // Bell, Cocoa & Stairs: Don't invert
            // Other Blocks: Don't invert
            var invertFacing = true;
            
            if (propTable.ContainsKey("attachment")) // Used by bell
            {
                predicateProps["attachment"] = targetDirection switch
                {
                    Direction.Up => "floor",
                    Direction.Down => "ceiling",
                    _ => "single_wall" // 
                };
                invertFacing = false;
            }
            
            if (propTable.ContainsKey("face"))
            {
                predicateProps["face"] = targetDirection switch
                {
                    Direction.Up => "floor",
                    Direction.Down => "ceiling",
                    _ => "wall"
                };
                invertFacing = targetDirection is not Direction.Up and not Direction.Down;
            }

            if (propTable.TryGetValue("facing", out var possibleValues))
            {
                if (blockId == COCOA_ID || blockId.Path.EndsWith("stairs") || blockId.Path.EndsWith("door")) invertFacing = false;
                
                if (possibleValues.Contains("up") && cameraPitch <= -44)
                {
                    predicateProps["facing"] = "up";
                }
                else if (possibleValues.Contains("down") && cameraPitch >= 44)
                {
                    predicateProps["facing"] = "down";
                }
                else if (ANVIL_IDS.Contains(blockId))
                {
                    predicateProps["facing"] = cameraYawDir switch
                    {
                        Direction.North => "east",
                        Direction.East => "south",
                        Direction.South => "west",
                        Direction.West => "north",
                        _ => throw new InvalidDataException($"Undefined direction {targetDirection}!")
                    };
                }
                else
                {
                    predicateProps["facing"] = targetDirection switch
                    {
                        Direction.North => invertFacing ? "north" : "south",
                        Direction.East => invertFacing ? "east" : "west",
                        Direction.South => invertFacing ? "south" : "north",
                        Direction.West => invertFacing ? "west" : "east",
                        _ => cameraYawDir switch
                        {
                            Direction.North => invertFacing ? "south" : "north",
                            Direction.East => invertFacing ? "west" : "east",
                            Direction.South => invertFacing ? "north" : "south",
                            Direction.West => invertFacing ? "east" : "west",
                            _ => throw new InvalidDataException($"Undefined direction {targetDirection}!")
                        }
                    };
                }
            }
            
            if (propTable.TryGetValue("type", out possibleValues))
            {
                if (possibleValues.Contains("bottom"))
                {
                    predicateProps["type"] = replace ? "double" : clickedTopHalf ? "top" : "bottom";
                }
            }

            if (propTable.TryGetValue("half", out possibleValues))
            {
                if (possibleValues.Contains("lower"))
                {
                    predicateProps["half"] = "lower";
                }
                else if (possibleValues.Contains("bottom"))
                {
                    predicateProps["half"] = clickedTopHalf ? "top" : "bottom";
                }
            }

            if (propTable.TryGetValue("axis", out possibleValues))
            {
                predicateProps["axis"] = targetDirection switch
                {
                    Direction.North => "z",
                    Direction.East => "x",
                    Direction.South => "z",
                    Direction.West => "x",
                    Direction.Up => possibleValues.Contains("y") ? "y" : (cameraYawDir == Direction.South || cameraYawDir == Direction.North ? "z" : "x"),
                    Direction.Down => possibleValues.Contains("y") ? "y" : (cameraYawDir == Direction.South || cameraYawDir == Direction.North ? "z" : "x"),
                    _ => throw new InvalidDataException($"Undefined direction {targetDirection}!")
                };
            }

            return palette.GetBlockStateWithProperties(blockId, predicateProps);
        }
    }
}