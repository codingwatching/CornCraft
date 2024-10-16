#nullable enable
using UnityEngine;

namespace CraftSharp.Rendering
{
    public class ItemEntityRender : EntityRender
    {
        public MeshFilter? itemMeshFilter;
        public MeshRenderer? itemMeshRenderer;

        public override void Initialize(Entity entity, Vector3Int originOffset)
        {
            base.Initialize(entity, originOffset);
            
            if (itemMeshFilter != null && itemMeshRenderer != null)
            {
                var result = ItemMeshBuilder.BuildItem(entity.Item);

                if (result != null) // If build suceeded
                {
                    itemMeshFilter.sharedMesh = result.Value.mesh;
                    itemMeshRenderer.sharedMaterial = result.Value.material;

                    // Apply random rotation
                    var meshTransform = itemMeshRenderer.transform;
                    meshTransform.localEulerAngles = new(0F, (entity!.ID * 350F) % 360F, 0F);

                    meshTransform.localPosition = new(0.5F, 0F, -0.5F);
                }
            }
            else
            {
                Debug.LogWarning("Item entity prefab components not assigned!");
            }
        }
    }
}