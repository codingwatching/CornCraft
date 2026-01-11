using UnityEngine;

namespace CraftSharp.Rendering
{
    [CreateAssetMenu(fileName = "ChunkMaterialPreset", menuName = "CornCraft/ChunkMaterialPreset")]
    public class ChunkMaterialPreset : ScriptableObject
    {
        public Material AtlasSolid;
        public Material AtlasCutout;
        public Material AtlasCutoutMipped;
        public Material AtlasTranslucent;
        public Material Water;
        public Material UnlitCutout;
        public Material Foliage;
        public Material Plants;

        public bool EnableFog;
        public bool UseAtlasForWater;
        
        public ChunkRenderBuilderSettings ChunkRenderBuilderSettings;
    }
}
