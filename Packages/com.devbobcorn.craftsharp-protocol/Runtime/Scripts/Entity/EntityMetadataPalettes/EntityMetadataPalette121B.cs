using System.Collections.Generic;

namespace CraftSharp
{
    /// <summary>
    /// 1.21.11
    /// </summary>
    public class EntityMetadataPalette121B : EntityMetadataPalette
    {
        private readonly Dictionary<int, EntityMetadataType> entityMetadataMappings = new()
        {
            { 0, EntityMetadataType.Byte },
            { 1, EntityMetadataType.VarInt },
            { 2, EntityMetadataType.VarLong },
            { 3, EntityMetadataType.Float },
            { 4, EntityMetadataType.String },
            { 5, EntityMetadataType.Chat },
            { 6, EntityMetadataType.OptionalChat },
            { 7, EntityMetadataType.Slot },
            { 8, EntityMetadataType.Boolean },
            { 9, EntityMetadataType.Rotations },
            { 10, EntityMetadataType.Position },
            { 11, EntityMetadataType.OptionalPosition },
            { 12, EntityMetadataType.Direction },
            { 13, EntityMetadataType.OptionalUUID },
            { 14, EntityMetadataType.BlockState },
            { 15, EntityMetadataType.OptionalBlockState },
            { 16, EntityMetadataType.Particle },
            { 17, EntityMetadataType.Particles },
            { 18, EntityMetadataType.VillagerData },
            { 19, EntityMetadataType.OptionalVarInt },
            { 20, EntityMetadataType.Pose },
            { 21, EntityMetadataType.CatVariant },
            { 22, EntityMetadataType.CowVariant },
            { 23, EntityMetadataType.WolfVariant },
            { 24, EntityMetadataType.WolfSoundVariant },
            { 25, EntityMetadataType.FrogVariant },
            { 26, EntityMetadataType.PigVariant },
            { 27, EntityMetadataType.ChickenVariant },
            { 28, EntityMetadataType.ZombieNautilusVariant }, // Added in 1.21.11
            { 29, EntityMetadataType.OptionalGlobalPosition },
            { 30, EntityMetadataType.PaintingVariant },
            { 31, EntityMetadataType.SnifferState },
            { 32, EntityMetadataType.ArmadilloState },
            { 33, EntityMetadataType.CopperGolemState },
            { 34, EntityMetadataType.WeatheringCopperState },
            { 35, EntityMetadataType.Vector3 },
            { 36, EntityMetadataType.Quaternion },
            { 37, EntityMetadataType.ResolvableProfile },
            { 38, EntityMetadataType.HumanoidArm } // Added in 1.21.11
        };
            
        public override Dictionary<int, EntityMetadataType> GetEntityMetadataMappingsList()
        {
            return entityMetadataMappings;
        }
    }
}