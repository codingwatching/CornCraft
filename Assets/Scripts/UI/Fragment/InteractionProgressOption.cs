using UnityEngine;
using UnityEngine.UI;

namespace CraftSharp.UI
{
    [RequireComponent(typeof (Animator))]
    public class InteractionProgressOption : InteractionOption
    {
        private static readonly int VALUE_AMOUNT = Shader.PropertyToID("_ValueAmount");
        private static readonly int DELTA_AMOUNT = Shader.PropertyToID("_DeltaAmount");
        
        [SerializeField] private Image barImage;
        private Material barMaterial;
        
        private void Start()
        {
            // Create a material instance for each bar
            barImage.material = new Material(barImage.material);
            barMaterial = barImage.materialForRendering;
            
            UpdateProgress(0F);
        }

        public void UpdateProgress(float progress)
        {
            barMaterial.SetFloat(VALUE_AMOUNT, progress);
            barMaterial.SetFloat(DELTA_AMOUNT, progress);
        }
    }
}