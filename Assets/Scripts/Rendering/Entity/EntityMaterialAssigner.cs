using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CraftSharp.Rendering
{
    [Serializable]
    public class EntityMaterialEntry
    {
        [SerializeField] public EntityRenderType RenderType;
        [SerializeField] private Material m_DefaultMaterial = null;
        [SerializeField] public string TextureId = string.Empty;
        [SerializeField] private Renderer[] m_Renderers;
        public Renderer[] Renderers => m_Renderers;
        
        #nullable enable

        public bool DynamicTextureId { get; private set; } = false;
        public List<(string, string)>? TextureIdVariables { get; private set; }
        public HashSet<int>? DependentMetaSlots { get; private set; }
        
        #nullable disable

        public EntityMaterialEntry(EntityRenderType renderType, Material material, Renderer[] renderers)
        {
            m_DefaultMaterial = material;
            RenderType = renderType;

            if (material && material.mainTexture)
            {
                TextureId = material.mainTexture.name;
            }

            m_Renderers = renderers;
        }

        public void SetupDynamicTextureId(EntityType entityType)
        {
            if (DynamicTextureId)
            {
                Debug.LogWarning("Dynamic texture already parsed!");
                return;
            }

            TextureIdVariables = new();
            DependentMetaSlots = new();

            // Turn texture id into a string template. e.g. entity/cow/{COLOR}_mooshroom
            const string pattern = @"\{.*?\}"; // Non-greedy matching using '?'

            TextureId = Regex.Replace(TextureId, pattern, m => convert(m.Value[1..^1]));

            if (TextureIdVariables.Count == 0)
            {
                Debug.LogWarning($"Malformed texture id: {TextureId}");
            }
            else
            {
                DynamicTextureId = true;
            }

            return;

            string convert(string variable)
            {
                var split = variable.Split('=');
                var variableName = split[0];
                var defaultValue = split.Length > 1 ? split[1] : "<missing>";

                if (variableName.StartsWith("meta@"))
                {
                    var metaName = variableName[5..].Split("@")[0];

                    if (!entityType.MetaEntriesByName.TryGetValue(metaName, out EntityMetaEntry metaEntry))
                    {
                        Debug.LogWarning($"{metaName} is not a valid entity metadata slot for {entityType}!");
                        return $"[{metaName}]";
                    }

                    var metaSlot = entityType.MetaSlotByName[metaEntry.Name];
                    DependentMetaSlots!.Add(metaSlot);
                    //Debug.Log($"{TextureId} depends on meta [{metaSlot}] {metaEntry.Name}");
                }
                /*
                else
                {
                    Debug.Log($"{TextureId} depends on variable named {variableName}");
                }
                */

                TextureIdVariables!.Add((variableName, defaultValue));
                
                return $"{{{TextureIdVariables.Count - 1}}}";
            }
        }
    }

    public class EntityMaterialAssigner : MonoBehaviour
    {
        [SerializeField] private EntityMaterialEntry[] m_MaterialEntries = { };
        public EntityMaterialEntry[] MaterialEntries => m_MaterialEntries;

        public float HurtTime { get; set; }  = 0F;
        private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
        private readonly Dictionary<Renderer, Color> m_OriginalColors = new();
        private MaterialPropertyBlock m_PropertyBlock;

        private void Awake()
        {
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (HurtTime > 0F)
            {
                HurtTime -= Time.deltaTime;
                
                if (HurtTime <= 0F)
                {
                    HurtTime = 0F;
                    // Restore original colors
                    RestoreOriginalColors();
                }
                else
                {
                    // Apply red tint
                    ApplyHurtTint();
                }
            }
        }

        private void ApplyHurtTint()
        {
            // Red-ish color for hurt effect
            var hurtColor = new Color(1F, 0.5F, 0.5F, 1F);

            foreach (var entry in m_MaterialEntries)
            {
                foreach (var r in entry.Renderers)
                {
                    if (!r) continue;

                    // Store original color if not already stored
                    if (!m_OriginalColors.ContainsKey(r))
                    {
                        var material = r.sharedMaterial;
                        if (material)
                        {
                            // Try to get color from _BaseColor or _Color property
                            if (material.HasProperty(BASE_COLOR))
                            {
                                m_OriginalColors[r] = material.GetColor(BASE_COLOR);
                            }
                            else
                            {
                                m_OriginalColors[r] = material.color;
                            }
                        }
                        else
                        {
                            m_OriginalColors[r] = Color.white;
                        }
                    }

                    // Apply hurt color using MaterialPropertyBlock
                    r.GetPropertyBlock(m_PropertyBlock);
                    
                    if (r.sharedMaterial)
                    {
                        if (r.sharedMaterial.HasProperty(BASE_COLOR))
                        {
                            m_PropertyBlock.SetColor(BASE_COLOR, hurtColor);
                        }
                    }
                    
                    r.SetPropertyBlock(m_PropertyBlock);
                }
            }
        }

        private void RestoreOriginalColors()
        {
            foreach (var entry in m_MaterialEntries)
            {
                foreach (var r in entry.Renderers)
                {
                    if (!r) continue;

                    if (m_OriginalColors.TryGetValue(r, out var originalColor))
                    {
                        // Clear property block to restore original material color
                        r.SetPropertyBlock(null);
                        m_OriginalColors.Remove(r);
                    }
                }
            }
        }

        public void InitializeRenderers()
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>();
            var entries = new Dictionary<Material, List<Renderer>>();

            foreach (var r in renderers)
            {
                if (!r.sharedMaterial) continue;

                if (!entries.ContainsKey(r.sharedMaterial))
                {
                    entries.Add(r.sharedMaterial, new List<Renderer>());
                }
                
                entries[r.sharedMaterial].Add(r);
            }

            m_MaterialEntries = entries.Select(x => new EntityMaterialEntry(
                    EntityRenderType.SOLID, x.Key, x.Value.ToArray() ) ).ToArray();
        }
        
        #nullable enable

        private static string GetVariableValue(EntityType entityType, string variableName, string defaultValue, Dictionary<string, string>? variables, Dictionary<int, object?>? metadata)
        {
            if (variableName.StartsWith("meta@"))
            {
                if (metadata is null)
                {
                    return defaultValue;
                }

                var metaEntrySplit = variableName[5..].Split('@', 2);
                var metaName = metaEntrySplit[0];

                if (!entityType.MetaEntriesByName.TryGetValue(metaName, out EntityMetaEntry metaEntry))
                {
                    Debug.LogWarning($"{metaName} is not a valid entity metadata slot for {entityType}!");
                    return defaultValue;
                }

                var metaSlot = entityType.MetaSlotByName[metaEntry.Name];
                var metaValue = metadata[metaSlot];

                if (metaValue is null)
                {
                    Debug.LogWarning($"Failed to get meta {metaName} at slot {metaSlot} for {entityType}!");
                    return defaultValue;
                }

                // Return the value directly
                return metaValue.ToString();
            }

            // Look in variable table
            var value = variables?.GetValueOrDefault(variableName, defaultValue);

            if (value is null)
            {
                Debug.LogWarning($"Failed to get variable {variableName} for {entityType}!");
                return defaultValue;
            }

            return value;
        }

        private static bool IsTextureIdAffected(EntityMaterialEntry entry, HashSet<string>? updatedVars, HashSet<int>? updatedMeta)
        {
            if (updatedVars is not null && entry.TextureIdVariables!.Any(x => updatedVars.Contains(x.Item1)))
            {
                return true;
            }

            return updatedMeta is not null && entry.DependentMetaSlots!.Any(updatedMeta.Contains);
        }

        /// <summary>
        /// Update materials after variable/updatedMeta value change
        /// </summary>
        public void UpdateMaterials(EntityType entityType, HashSet<string>? updatedVars, HashSet<int>? updatedMeta, Dictionary<string, string>? variables, Dictionary<int, object?>? metadata)
        {
            var client = CornApp.CurrentClient!;
            var matManager = client.EntityMaterialManager;

            //var info = updatedMeta.Select(x => entityType.MetaEntries[x].Name + ": [" + metadata?[x] + "],");
            //Debug.Log($"Updating meta ({gameObject.name}):\n{string.Join("\n", info)}");

            for (int i = 0; i < m_MaterialEntries.Length; i++)
            {
                var entry = m_MaterialEntries[i];
                ResourceLocation textureId;

                if (entry.DynamicTextureId && IsTextureIdAffected(entry, updatedVars, updatedMeta))
                {
                    var vars = entry.TextureIdVariables!.Select(x =>
                        (object) GetVariableValue(entityType, x.Item1, x.Item2, variables, metadata)).ToArray();
                    var interpolated = string.Format(entry.TextureId, vars);
                    //Debug.Log($"Updating texture {entry.TextureId} with {string.Join(", ", vars)}");
                    textureId = ResourceLocation.FromString(interpolated);
                }
                else
                {
                    // Not affected, skip.
                    continue;
                }

                matManager.ApplyMaterial(entry.RenderType, textureId, matInstance =>
                {
                    AssignMaterialToRenderer(entry.Renderers, matInstance);
                });
            }
        }

        private void AssignMaterialToRenderer(Renderer[] renderers, Material matInstance)
        {
            for (int j = 0; j < renderers.Length; j++)
            {
                // Clear stored original color if material is being reassigned
                if (m_OriginalColors.ContainsKey(renderers[j]))
                {
                    m_OriginalColors.Remove(renderers[j]);
                }
                
                renderers[j].sharedMaterial = matInstance;
            }
        }

        public void InitializeMaterials(EntityType entityType, Dictionary<string, string>? variables, Action<EntityMaterialManager, ResourceLocation, Material> callbackForEach)
        {
            var client = CornApp.CurrentClient!;
            var matManager = client.EntityMaterialManager;

            for (int i = 0; i < m_MaterialEntries.Length; i++)
            {
                var entry = m_MaterialEntries[i];
                ResourceLocation textureId;

                if (entry.TextureId.Contains('{'))
                {
                    // Extract updatedVars in texture id
                    entry.SetupDynamicTextureId(entityType);
                }

                if (entry.DynamicTextureId)
                {
                    // Metadata is not available during initialization(will be sent afterwards)
                    var vars = entry.TextureIdVariables!.Select(x =>
                            (object) GetVariableValue(entityType, x.Item1, x.Item2, variables, null)).ToArray();
                    var interpolated = string.Format(entry.TextureId, vars);
                    textureId = ResourceLocation.FromString(interpolated);
                }
                else
                {
                    textureId = ResourceLocation.FromString(entry.TextureId);
                }
                
                matManager.ApplyMaterial(entry.RenderType, textureId, matInstance =>
                {
                    AssignMaterialToRenderer(entry.Renderers, matInstance);

                    callbackForEach.Invoke(matManager, textureId, matInstance);
                });
            }
        }
    }
}