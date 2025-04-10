using System;
using UnityEngine;
using TMPro;

using CraftSharp.Protocol;
using CraftSharp.Rendering;

namespace CraftSharp.UI
{
    [RequireComponent(typeof (Animator))]
    public class EntityNameUI : FloatingUI
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        private Action destroyCallback;

        public override void SetInfo(EntityRender entityRender)
        {
            this.entityRender = entityRender;

            if (nameText != null)
            {
                nameText.text = (entityRender.Name ?? entityRender.CustomName) ??
                        ChatParser.TranslateString(entityRender.Type.TypeId.GetTranslationKey("entity"));

            }

            if (descriptionText != null)
            {
                descriptionText.text = $"<{entityRender.Type.TypeId}>";
            }
        }

        public override void Destroy(Action callback)
        {
            var animator = GetComponent<Animator>();
            animator.SetBool("Expired", true);

            // Store this for later invocation
            destroyCallback = callback;
        }

        // Called by animator aftering fading out
        void Expire()
        {
            destroyCallback?.Invoke();
            Destroy(gameObject);
        }
    }
}