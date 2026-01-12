#nullable enable
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using CraftSharp.Resource;
using Object = UnityEngine.Object;

namespace CraftSharp.Rendering
{
    public static class BlockMeshBuilder
    {
        private static readonly Mesh EMPTY_BLOCK_MESH = new();
        
        private static readonly float[] FULL_CORNER_LIGHTS = Enumerable.Repeat(1F, 8).ToArray();
        private static readonly byte[] FLUID_HEIGHTS = Enumerable.Repeat((byte) 15, 9).ToArray();
        
        private static readonly LegacyRandomSource randomSource = new(0L);
        
        private static void ClearBlockVisual(GameObject modelObject)
        {
            // Clear mesh if present
            if (modelObject.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                meshFilter.sharedMesh = null;
            }
            
            // Clear children if present
            foreach (Transform t in modelObject.transform)
            {
                Object.Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// Build item object from item stack. Returns true if item is not empty and successfully built
        /// </summary>
        public static void BuildBlockGameObject(GameObject modelObject, BlockState blockState, World world)
        {
            ClearBlockVisual(modelObject);
            
            if (!modelObject.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                meshFilter = modelObject.AddComponent<MeshFilter>();
            }

            if (!modelObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
            {
                meshRenderer = modelObject.AddComponent<MeshRenderer>();
            }

            var blockId = blockState.BlockId;
            var stateId = BlockStatePalette.INSTANCE.GetNumIdByObject(blockState);

            var client = CornApp.CurrentClient;
            if (!client) return;
            
            var packManager = ResourcePackManager.Instance;
            packManager.StateModelTable.TryGetValue(stateId, out var stateModel);
            if (stateModel is null) return;
            
            var blockGeometry = stateModel.Geometries[0];

            if (BlockEntityTypePalette.INSTANCE.GetBlockEntityForBlock(blockId, out var blockEntityType)) // Use embedded entity render
            {
                client.ChunkRenderManager.CreateBlockEntityRenderForItemModel(modelObject.transform, blockState, blockEntityType);
            }
            
            var color = BlockStatePalette.INSTANCE.GetBlockColor(stateId, world, BlockLoc.Zero);
            var waterColor = world.GetWaterColor(BlockLoc.Zero);
            
            // Use regular mesh
            var mesh = BuildBlockMesh_Internal(blockState, blockGeometry, float3.zero, 0b111111, color, waterColor, 0, true);
            
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = client.ChunkMaterialManager.GetAtlasMaterial(stateModel.RenderType);

            if (blockState.InWater)
            {
                meshRenderer.sharedMaterials = new [] {
                        client.ChunkMaterialManager.GetAtlasMaterial(RenderType.WATER),
                        client.ChunkMaterialManager.GetAtlasMaterial(stateModel.RenderType) };
            }
            else
            {
                meshRenderer.sharedMaterial = client.ChunkMaterialManager.GetAtlasMaterial(stateModel.RenderType);
            }
        }

        /// <summary>
        /// Build block mesh.
        /// </summary>
        public static (Mesh, RenderType) BuildBlockMesh(BlockState blockState, float3 posOffset, long randomSeed, int cullFlags, int colorInt, byte blockLight, int waterColorInt, bool includeLiquidMesh)
        {
            var packManager = ResourcePackManager.Instance;
            var stateId = BlockStatePalette.INSTANCE.GetNumIdByObject(blockState);

            packManager.StateModelTable.TryGetValue(stateId, out var stateModel);

            if (stateModel is null) return (EMPTY_BLOCK_MESH, RenderType.SOLID);

            // Get and build chosen variant
            var models = stateModel.Geometries;
            randomSource.SetSeed(randomSeed);
            var chosen = ModuloUtil.Modulo(Mathf.Abs((int) randomSource.NextLong()), models.Length);
            var blockGeometry = models[chosen];

            var mesh = BuildBlockMesh_Internal(blockState, blockGeometry, posOffset, cullFlags, colorInt, waterColorInt, blockLight, includeLiquidMesh);

            return (mesh, stateModel.RenderType);
        }
        
        private static Mesh BuildBlockMesh_Internal(BlockState state, BlockGeometry geometry, float3 posOffset, int cullFlags, int colorInt, int waterColorInt, byte blockLight, bool includeLiquidMesh)
        {
            var vertexCount = geometry.GetVertexCount(cullFlags);
            var fluidVertexCount = 0;

            if (includeLiquidMesh && state.InLiquid)
            {
                fluidVertexCount = FluidGeometry.GetVertexCount(cullFlags);
                vertexCount += fluidVertexCount;
            }
            
            // Make and set mesh...
            var visualBuffer = new VertexBuffer(vertexCount);

            uint vertexOffset = 0;
            var blockVertLight = Enumerable.Repeat((float) blockLight, 8).ToArray();

            if (includeLiquidMesh)
            {
                if (state.InWater)
                {
                    FluidGeometry.Build(visualBuffer, ref vertexOffset, float3.zero, FluidGeometry.LiquidTextures[0], FLUID_HEIGHTS,
                        cullFlags, blockVertLight, waterColorInt);
                }
                else if (state.InLava)
                    FluidGeometry.Build(visualBuffer, ref vertexOffset, float3.zero, FluidGeometry.LiquidTextures[1], FLUID_HEIGHTS,
                        cullFlags, blockVertLight, 0xFFFFFF);
            }

            geometry.Build(visualBuffer, ref vertexOffset, posOffset, cullFlags, 0, 0F, blockVertLight, colorInt);

            var triIdxCount = vertexCount / 2 * 3;

            var meshDataArr = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArr[0];

            var vertAttrs = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vertAttrs[0] = new(VertexAttribute.Position,  dimension: 3, stream: 0);
            vertAttrs[1] = new(VertexAttribute.TexCoord0, dimension: 3, stream: 1);
            vertAttrs[2] = new(VertexAttribute.TexCoord3, dimension: 4, stream: 2);
            vertAttrs[3] = new(VertexAttribute.Color,     dimension: 4, stream: 3);

            // Set mesh params
            meshData.SetVertexBufferParams(vertexCount, vertAttrs);
            vertAttrs.Dispose();

            meshData.SetIndexBufferParams(triIdxCount, IndexFormat.UInt32);

            // Set vertex data
            // Positions
            var positions = meshData.GetVertexData<float3>(0);
            positions.CopyFrom(visualBuffer.vert);
            // Tex Coordinates
            var texCoords = meshData.GetVertexData<float3>(1);
            texCoords.CopyFrom(visualBuffer.txuv);
            // Animation Info
            var animInfos = meshData.GetVertexData<float4>(2);
            animInfos.CopyFrom(visualBuffer.uvan);
            // Vertex colors
            var vertColors = meshData.GetVertexData<float4>(3);
            vertColors.CopyFrom(visualBuffer.tint);

            // Set face data
            var triIndices = meshData.GetIndexData<uint>();
            uint vi = 0; var ti = 0;
            for (; vi < vertexCount; vi += 4U, ti += 6)
            {
                triIndices[ti]     = vi;
                triIndices[ti + 1] = vi + 3U;
                triIndices[ti + 2] = vi + 2U;
                triIndices[ti + 3] = vi;
                triIndices[ti + 4] = vi + 1U;
                triIndices[ti + 5] = vi + 3U;
            }

            var bounds = new Bounds(new Vector3(0.5F, 0.5F, 0.5F), new Vector3(1F, 1F, 1F));

            if (includeLiquidMesh && state.InLiquid)
            {
                var fluidTriIdxCount = fluidVertexCount / 2 * 3;

                meshData.subMeshCount = 2;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, fluidTriIdxCount)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshUpdateFlags.DontRecalculateBounds);
                meshData.SetSubMesh(1, new SubMeshDescriptor(fluidTriIdxCount, triIdxCount - fluidTriIdxCount)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshUpdateFlags.DontRecalculateBounds);
            }
            else
            {
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, triIdxCount)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshUpdateFlags.DontRecalculateBounds);
            }

            // Create and assign mesh
            var mesh = new Mesh { bounds = bounds };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArr, mesh);
            // Recalculate mesh normals
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Build break block mesh.
        /// </summary>
        public static Mesh BuildBlockBreakMesh(BlockState blockState, float3 posOffset, int cullFlags)
        {
            var packManager = ResourcePackManager.Instance;
            var stateId = BlockStatePalette.INSTANCE.GetNumIdByObject(blockState);

            packManager.StateModelTable.TryGetValue(stateId, out var stateModel);

            if (stateModel is null) return EMPTY_BLOCK_MESH;

            // Get and build the first geometry
            var blockGeometry = stateModel.Geometries[0];

            var mesh = BuildBlockBreakMesh_Internal(blockGeometry, posOffset, cullFlags);

            return mesh;
        }

        /// <summary>
        /// Build block break mesh directly from block geometry.
        /// </summary>
        private static Mesh BuildBlockBreakMesh_Internal(BlockGeometry blockGeometry, float3 posOffset, int cullFlags)
        {
            var vertexCount = blockGeometry.GetVertexCount(cullFlags);
            var visualBuffer = new VertexBuffer(vertexCount);
            uint vertexOffset = 0;
            blockGeometry.Build(visualBuffer, ref vertexOffset, posOffset, cullFlags, 0, 0F, FULL_CORNER_LIGHTS,
                0, BlockGeometry.VertexDataFormat.ExtraUV_Light);

            var triIdxCount = vertexCount / 2 * 3;

            var meshDataArr = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArr[0];

            var vertAttrs = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vertAttrs[0] = new(VertexAttribute.Position,  dimension: 3, stream: 0);
            vertAttrs[1] = new(VertexAttribute.TexCoord0, dimension: 3, stream: 1);
            vertAttrs[2] = new(VertexAttribute.Color,     dimension: 4, stream: 2);

            // Set mesh params
            meshData.SetVertexBufferParams(vertexCount, vertAttrs);
            vertAttrs.Dispose();

            meshData.SetIndexBufferParams(triIdxCount, IndexFormat.UInt32);

            // Set vertex data
            // Positions
            var positions = meshData.GetVertexData<float3>(0);
            positions.CopyFrom(visualBuffer.vert);
            // Tex Coordinates
            var texCoords = meshData.GetVertexData<float3>(1);
            texCoords.CopyFrom(visualBuffer.txuv);
            // Vertex colors
            var vertColors = meshData.GetVertexData<float4>(2);
            vertColors.CopyFrom(visualBuffer.tint);

            // Set face data
            var triIndices = meshData.GetIndexData<uint>();
            uint vi = 0; var ti = 0;
            for (;vi < vertexCount;vi += 4U, ti += 6)
            {
                triIndices[ti]     = vi;
                triIndices[ti + 1] = vi + 3U;
                triIndices[ti + 2] = vi + 2U;
                triIndices[ti + 3] = vi;
                triIndices[ti + 4] = vi + 1U;
                triIndices[ti + 5] = vi + 3U;
            }

            var bounds = new Bounds(new Vector3(0.5F, 0.5F, 0.5F), new Vector3(1F, 1F, 1F));

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triIdxCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh
            {
                bounds = bounds,
                name = "Proc Mesh"
            };

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArr, mesh);

            // Recalculate mesh bounds and normals
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}