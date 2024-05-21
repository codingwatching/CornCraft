using System.Collections.Generic;
using UnityEngine;

namespace CraftSharp.Rendering
{
    public class ChunkRenderColumn : MonoBehaviour
    {
        public int ChunkX, ChunkZ;

        private readonly Dictionary<int, ChunkRender> chunks = new Dictionary<int, ChunkRender>();

        private ChunkRender CreateChunkRender(int chunkY)
        {
            // Create this chunk...
            GameObject chunkObj = new GameObject($"Chunk [{chunkY}]");
            chunkObj.layer = UnityEngine.LayerMask.NameToLayer(ChunkRenderManager.SOLID_LAYER_NAME);
            ChunkRender newChunk = chunkObj.AddComponent<ChunkRender>();
            newChunk.ChunkX = this.ChunkX;
            newChunk.ChunkY = chunkY;
            newChunk.ChunkZ = this.ChunkZ;
            // Set its parent to this chunk column...
            chunkObj.transform.parent = this.transform;
            chunkObj.transform.localPosition = CoordConvert.MC2Unity(this.ChunkX * Chunk.SIZE, chunkY * Chunk.SIZE + World.GetDimension().minY, this.ChunkZ * Chunk.SIZE);
            
            return newChunk;
        }

        public bool HasChunkRender(int chunkY) => chunks.ContainsKey(chunkY);

        public Dictionary<int, ChunkRender> GetChunkRenders() => chunks;

        public ChunkRender GetChunkRender(int chunkY, bool createIfEmpty)
        {
            if (chunks.ContainsKey(chunkY))
            {
                return chunks[chunkY];
            }
            else
            {
                // This chunk doesn't currently exist...
                if (chunkY >= 0 && chunkY * Chunk.SIZE < World.GetDimension().height)
                {
                    if (createIfEmpty)
                    {
                        ChunkRender newChunk = CreateChunkRender(chunkY);
                        chunks.Add(chunkY, newChunk);
                        return newChunk;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    //Debug.Log("Trying to get a chunk at invalid height: " + chunkY);
                    return null;
                }
            }
        }

        /// <summary>
        /// Unload a chunk render, accessible on unity thread only
        /// </summary>
        /// <param name="chunksBeingBuilt"></param>
        /// <param name="chunks2Build"></param>
        public void Unload(ref List<ChunkRender> chunksBeingBuilt, ref PriorityQueue<ChunkRender> chunks2Build)
        {
            // Unload this chunk column...
            foreach (int i in chunks.Keys)
            {
                var chunk = chunks[i];

                // Unload all chunks in this column, except empty chunks...
                if (chunk != null)
                {   // Before destroying the chunk object, do one last thing
                    

                    if (chunks2Build.Contains(chunk))
                        chunks2Build.Remove(chunk);
                    
                    chunksBeingBuilt.Remove(chunk);
                    chunk.Unload();
                }

            }
            chunks.Clear();

            if (this != null)
                Destroy(this.gameObject);
        }

        public override string ToString() => $"[ChunkRenderColumn {ChunkX}, {ChunkZ}]";
    }
}