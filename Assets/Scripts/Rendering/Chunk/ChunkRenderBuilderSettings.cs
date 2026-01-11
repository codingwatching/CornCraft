using System;
using CraftSharp.Resource;

namespace CraftSharp.Rendering
{
    [Serializable]
    public struct ChunkRenderBuilderSettings
    {
        public BlockGeometry.VertexDataFormat FoliageVertexDataFormat;
        public BlockGeometry.VertexDataFormat PlantsVertexDataFormat;
        
        public float AOIntensity;
        public float FoliageAOIntensity;
        public float PlantsAOIntensity;
    }
}