using UnityEngine;
using UnityEngine.UI;

namespace CraftSharp.UI
{
    public class ServerListPanel : MonoBehaviour
    {
        [SerializeField] private Button      closeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>
        /// Hides the panel
        /// </summary>
        public void Hide()
        {
            canvasGroup.alpha = 0F;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void Start()
        {
            // Initialize server list panel as hidden
            Hide();

            // Add listeners
            closeButton.onClick.AddListener(Hide);
        }
    }
}