using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CraftSharp.UI
{
    [RequireComponent(typeof (CanvasGroup))]
    public class PauseScreen : BaseScreen
    {
        // UI controls and objects
        [SerializeField] private Button resumeButton, quitButton;
        [SerializeField] private Animator screenAnimator;

        private bool isActive = false;

        public override bool IsActive
        {
            set {
                isActive = value;
                screenAnimator.SetBool(SHOW_HASH, isActive);
            }

            get => isActive;
        }

        public override bool ReleaseCursor()
        {
            return true;
        }

        public override bool ShouldPauseControllerInput()
        {
            return true;
        }

        private static void CloseScreen()
        {
            var client = CornApp.CurrentClient;
            if (!client) return;

            client.ScreenControl.TryPopScreen();
        }

        private static void QuitToLogin()
        {
            var client = CornApp.CurrentClient;
            if (!client) return;

            client.Disconnect();
        }

        protected override void Initialize()
        {
            // Initialize controls and add listeners
            resumeButton.onClick.AddListener(CloseScreen);
            quitButton.onClick.AddListener(QuitToLogin);
        }

        public override void UpdateScreen()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseScreen();
            }
        }
    }
}
