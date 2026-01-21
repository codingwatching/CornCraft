using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace CraftSharp.UI
{
    /// <summary>
    /// Manages the Microsoft authentication panel UI and related user interactions
    /// </summary>
    public class AuthPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField authCodeInput;
        [SerializeField] private Button         confirmButton, cancelButton;
        [SerializeField] private Button         authLinkButton, closeButton;
        [SerializeField] private TMP_Text       authLinkText;
        [SerializeField] private CanvasGroup    canvasGroup;

        #nullable enable
        private System.Action? onAuthConfirmed;
        private System.Action? onAuthCancelled;
        #nullable disable

        public bool IsAuthenticating { get; private set; } = false;
        public bool WasAuthCancelled { get; private set; } = false;

        private void Start()
        {
            // Initialize auth panel as hidden
            Hide();

            // Add listeners
            authLinkButton.onClick.AddListener(CopyAuthLink);
            cancelButton.onClick.AddListener(CancelAuth);
            confirmButton.onClick.AddListener(ConfirmAuth);
            authCodeInput.GetComponentInChildren<Button>().onClick.AddListener(PasteAuthCode);
            closeButton.onClick.AddListener(CancelAuth);
        }

        /// <summary>
        /// Shows the authentication panel with the given Microsoft auth URL
        /// </summary>
        public void Show(string url)
        {
            // Update auth panel link text
            authLinkText.text = url;

            // Clear existing text if any
            authCodeInput.text = string.Empty;

            canvasGroup.alpha = 1F;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            IsAuthenticating = true;
            WasAuthCancelled = false;
        }

        /// <summary>
        /// Hides the panel
        /// </summary>
        public void Hide()
        {
            canvasGroup.alpha = 0F;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            IsAuthenticating = false;
        }

        /// <summary>
        /// Gets the entered authentication code
        /// </summary>
        public string GetAuthCode()
        {
            return authCodeInput.text.Trim();
        }

        /// <summary>
        /// Sets callbacks for authentication confirmation and cancellation
        /// </summary>
        public void SetAuthCallbacks(System.Action onConfirmed, System.Action onCancelled)
        {
            onAuthConfirmed = onConfirmed;
            onAuthCancelled = onCancelled;
        }

        private void CopyAuthLink()
        {
            GUIUtility.systemCopyBuffer = authLinkText.text;
            CornApp.Notify(Translations.Get("login.link_copied"), Notification.Type.Success);
        }

        private void PasteAuthCode()
        {
            authCodeInput.text = GUIUtility.systemCopyBuffer;
        }

        private void CancelAuth()
        {
            WasAuthCancelled = true;
            Hide();
            onAuthCancelled?.Invoke();
        }

        private void ConfirmAuth()
        {
            var code = GetAuthCode();

            if (string.IsNullOrEmpty(code))
            {
                CornApp.Notify(Translations.Get("login.auth_code_empty"), Notification.Type.Warning);
                return;
            }

            WasAuthCancelled = false;
            Hide();
            onAuthConfirmed?.Invoke();
        }
    }
}
