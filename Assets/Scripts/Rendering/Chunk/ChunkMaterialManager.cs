using System.Collections.Generic;
using UnityEngine;

using CraftSharp.Resource;

namespace CraftSharp.Rendering
{
    public class ChunkMaterialManager : MonoBehaviour
    {
        private static readonly int BASE_MAP_HASH = Shader.PropertyToID("_BaseMap");
        
        [SerializeField] private ChunkMaterialPreset chunkMaterialPreset;
        public ChunkMaterialPreset ChunkMaterialPreset => chunkMaterialPreset;

        private readonly Dictionary<RenderType, Material> atlasMaterials = new();
        private readonly Dictionary<RenderType, Material> inventoryMaterials = new();
        private Material defaultAtlasMaterial;

        private bool atlasMaterialsInitialized = false;

        public Material GetAtlasMaterial(RenderType renderType, bool getInventoryVariant = false)
        {
            EnsureAtlasMaterialsInitialized();

            if (getInventoryVariant)
            {
                return inventoryMaterials.GetValueOrDefault(renderType, defaultAtlasMaterial);
            }

            return atlasMaterials.GetValueOrDefault(renderType, defaultAtlasMaterial);
        }

        public void EnsureAtlasMaterialsInitialized()
        {
            if (!atlasMaterialsInitialized) Initialize();
        }

        private void Initialize()
        {
            atlasMaterials.Clear();
            inventoryMaterials.Clear();
            var packManager = ResourcePackManager.Instance;

            // Solid
            var solid = new Material(chunkMaterialPreset.AtlasSolid);
            solid.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            atlasMaterials.Add(RenderType.SOLID, solid);

            defaultAtlasMaterial = solid;

            // Cutout & Cutout Mipped
            var cutout = new Material(chunkMaterialPreset.AtlasCutout);
            cutout.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(false));
            atlasMaterials.Add(RenderType.CUTOUT, cutout);

            var cutoutMipped = new Material(chunkMaterialPreset.AtlasCutoutMipped);
            cutoutMipped.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            atlasMaterials.Add(RenderType.CUTOUT_MIPPED, cutoutMipped);

            // Translucent
            var translucent = new Material(chunkMaterialPreset.AtlasTranslucent);
            translucent.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            atlasMaterials.Add(RenderType.TRANSLUCENT, translucent);
            
            // Unlit
            var unlit = new Material(chunkMaterialPreset.UnlitCutout);
            unlit.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            atlasMaterials.Add(RenderType.UNLIT, unlit);

            // Water
            var water = new Material(chunkMaterialPreset.Water);
            if (chunkMaterialPreset.UseAtlasForWater)
            {
                water.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            }
            atlasMaterials.Add(RenderType.WATER, water);

            // Foliage
            var foliage = new Material(chunkMaterialPreset.Foliage);
            foliage.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(true));
            atlasMaterials.Add(RenderType.FOLIAGE, foliage);

            // Plants & Tall Plants
            var plants = new Material(chunkMaterialPreset.Plants);
            plants.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(false));
            atlasMaterials.Add(RenderType.PLANTS, plants);

            var tallPlants = new Material(chunkMaterialPreset.Plants);
            tallPlants.SetTexture(BASE_MAP_HASH, packManager.GetAtlasArray(false));
            atlasMaterials.Add(RenderType.TALL_PLANTS, tallPlants);

            // Inventory Solid
            var inventorySolid = new Material(solid);
            inventorySolid.name += " (Inventory)";
            inventorySolid.EnableKeyword("_DISABLE_FOG_AND_GI");
            inventoryMaterials.Add(RenderType.SOLID, inventorySolid);

            // Inventory Cutout (Mipping Not Required Here)
            var inventoryCutout = new Material(cutout);
            inventoryCutout.name += " (Inventory)";
            inventoryCutout.EnableKeyword("_DISABLE_FOG_AND_GI");
            inventoryMaterials.Add(RenderType.CUTOUT, inventoryCutout);
            inventoryMaterials.Add(RenderType.CUTOUT_MIPPED, inventoryCutout);

            // Inventory Translucent
            var inventoryTranslucent = new Material(translucent);
            inventoryTranslucent.name += " (Inventory)";
            inventoryTranslucent.EnableKeyword("_DISABLE_FOG_AND_GI");
            inventoryMaterials.Add(RenderType.TRANSLUCENT, inventoryTranslucent);
            
            // Inventory Unlit
            var inventoryUnlit = new Material(unlit);
            inventoryUnlit.name += " (Inventory)";
            inventoryUnlit.EnableKeyword("_DISABLE_FOG_AND_GI");
            inventoryMaterials.Add(RenderType.UNLIT, inventoryUnlit);

            // Inventory Water - Shouldn't be used
            inventoryMaterials.Add(RenderType.WATER, water);

            // Inventory Foliage - Use Inventory Cutout instead
            inventoryMaterials.Add(RenderType.FOLIAGE, inventoryCutout);

            // Inventory Plants & Inventory Tall Plants - Use Inventory Cutout instead
            inventoryMaterials.Add(RenderType.PLANTS, inventoryCutout);
            inventoryMaterials.Add(RenderType.TALL_PLANTS, inventoryCutout);

            atlasMaterialsInitialized = true;
        }
    }
}