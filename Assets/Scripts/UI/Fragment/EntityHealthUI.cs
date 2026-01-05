using UnityEngine;
using TMPro;

using CraftSharp.Rendering;

namespace CraftSharp.UI
{
    public class EntityHealthUI : FloatingUI
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private BaseValueBar healthBar;

        private void UpdateMaxHealth(float prevVal, float newVal)
        {
            if (healthBar)
            {
                healthBar.MaxValue = newVal;
            }
        }
        
        private void UpdateHealth(float prevVal, float newVal)
        {
            if (healthBar)
            {
                healthBar.CurValue = newVal;
            }
        }

        private void OnDestroy()
        {
            // Unregister events for previous entity
            if (entityRender)
            {
                entityRender.MaxHealth.OnValueUpdate -= UpdateMaxHealth;
                entityRender.Health.OnValueUpdate -= UpdateHealth;
            }
        }

        public override void SetInfo(EntityRender sourceEntityRender)
        {
            // Unregister events for previous entity
            // NOTE: It is not recommended to call SetInfo for more than one entity
            if (entityRender)
            {
                entityRender.MaxHealth.OnValueUpdate -= UpdateMaxHealth;
                entityRender.Health.OnValueUpdate -= UpdateHealth;
            }

            entityRender = sourceEntityRender;

            // Register events for new entity
            if (entityRender)
            {
                entityRender.MaxHealth.OnValueUpdate += UpdateMaxHealth;
                entityRender.Health.OnValueUpdate += UpdateHealth;

                UpdateHealth(0F, entityRender.Health.Value);
                UpdateMaxHealth(0F, entityRender.MaxHealth.Value);
            }

            if (nameText)
            {
                // This is used for mimicking the UI format of some anime game
                // This text is no longer updated after first set
                nameText.text = sourceEntityRender.GetDisplayName();
            }
        }
    }
}