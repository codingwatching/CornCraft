using UnityEngine;

namespace CraftSharp.Rendering
{
    [CreateAssetMenu(fileName = "EntityMaterialPreset", menuName = "CornCraft/EntityMaterialPreset")]
    public class EntityMaterialPreset : ScriptableObject
    {
        public Material EntitySolid;
        public Material EntityCutout;
        public Material EntityCutoutDoubleSided;
        public Material EntityTranslucent;
    }
}
