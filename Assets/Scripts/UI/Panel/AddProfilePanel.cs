using System;
using System.Net.Mail;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CraftSharp.Protocol;

namespace CraftSharp.UI
{
    public class AddProfilePanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_InputField loginNameInput;
        [SerializeField] private TMP_Dropdown profileTypeDropdown;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup canvasGroup;

        public event Action<UserProfile> ProfileAdded;


        /// <summary>
        /// Shows the panel
        /// </summary>
        public void Show()
        {
            canvasGroup.alpha = 1F;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        /// <summary>
        /// Hides the panel
        /// </summary>
        public void Hide()
        {
            canvasGroup.alpha = 0F;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void UpdateLoginPlaceholder(int value)
        {
            loginNameInput.placeholder.GetComponent<TMP_Text>().text =
                Translations.Get(value == 0 ? "login.placeholder_email" : "login.placeholder_playername");
        }

        private void Start()
        {
            // Initialize add profile panel as hidden
            Hide();

            // Add listeners
            closeButton.onClick.AddListener(Hide);
            profileTypeDropdown.onValueChanged.AddListener(UpdateLoginPlaceholder);
            confirmButton.onClick.AddListener(ConfirmAddProfile);

            // Set initial placeholder
            UpdateLoginPlaceholder(profileTypeDropdown.value);
        }

        private void ConfirmAddProfile()
        {
            var loginName = loginNameInput.text.Trim();
            if (string.IsNullOrWhiteSpace(loginName))
            {
                CornApp.Notify(Translations.Get("login.profile_name_empty"), Notification.Type.Warning);
                return;
            }

            var profileType = profileTypeDropdown.value == 0
                ? UserProfileType.Microsoft
                : UserProfileType.Offline;
            
            if (profileType == UserProfileType.Microsoft && !IsValidEmail(loginName))
            {
                CornApp.Notify(Translations.Get("login.email_invalid"), Notification.Type.Warning);
                return;
            }

            if (profileType == UserProfileType.Offline && !PlayerInfo.IsValidName(loginName))
            {
                CornApp.Notify(Translations.Get("login.offline_username_invalid"), Notification.Type.Warning);
                return;
            }

            var profile = new UserProfile
            {
                LoginName = loginName,
                Type = profileType,
                MicrosoftRefreshToken = string.Empty
            };

            UserProfileManager.AddOrUpdateProfile(profile);
            ProfileAdded?.Invoke(profile);

            loginNameInput.text = string.Empty;
            Hide();
        }

        private static bool IsValidEmail(string loginName)
        {
            try
            {
                var address = new MailAddress(loginName);
                return string.Equals(address.Address, loginName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}