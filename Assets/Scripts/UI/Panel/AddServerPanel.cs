using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CraftSharp.Protocol;

namespace CraftSharp.UI
{
    public class AddServerPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_InputField serverNameInput;
        [SerializeField] private TMP_InputField serverAddressInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private CanvasGroup canvasGroup;

        public event Action<ServerRecord> ServerAdded;


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

        private void Start()
        {
            // Initialize add server panel as hidden
            Hide();

            // Add listeners
            closeButton.onClick.AddListener(Hide);
            confirmButton.onClick.AddListener(ConfirmAddServer);
        }

        private void ConfirmAddServer()
        {
            var serverName = serverNameInput.text.Trim();
            if (string.IsNullOrWhiteSpace(serverName))
            {
                CornApp.Notify(Translations.Get("login.server_name_empty"), Notification.Type.Warning);
                return;
            }

            var serverAddress = serverAddressInput.text.Trim();
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                CornApp.Notify(Translations.Get("login.server_address_empty"), Notification.Type.Warning);
                return;
            }

            if (!ParseServerIP(serverAddress, out var host, out var port) || host is null)
            {
                CornApp.Notify(Translations.Get("login.server_address_invalid"), Notification.Type.Warning);
                return;
            }

            var serverRecord = new ServerRecord
            {
                DisplayName = serverName,
                Address = host,
                Port = port
            };

            ServerRecordManager.AddOrUpdateServer(serverRecord);
            ServerAdded?.Invoke(serverRecord);

            serverNameInput.text = string.Empty;
            serverAddressInput.text = string.Empty;
            Hide();
        }

        private static bool ParseServerIP(string server, out string host, out ushort port)
        {
            server = server.ToLower();
            string[] sip = server.Split(':');
            host = sip[0];
            port = 25565;

            if (sip.Length > 1)
            {
                if (sip.Length == 2) // IPv4 with port
                {
                    try
                    {
                        port = Convert.ToUInt16(sip[1]);
                    }
                    catch (FormatException) { return false; }
                }
                else // IPv6 address maybe
                {
                    server = server.TrimStart('[');
                    sip = server.Split(']');
                    host = sip[0];

                    if (sip.Length > 1)
                    {
                        if (sip.Length == 2) // IPv6 with port
                        {
                            try
                            {
                                // Trim ':' before port
                                port = Convert.ToUInt16(sip[1].TrimStart(':'));
                            }
                            catch (FormatException) { return false; }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            if (host == "localhost" || host.Contains('.') || host.Contains(':'))
            {
                if (sip.Length == 1 && host.Contains('.') && host.Any(char.IsLetter) && ProtocolSettings.ResolveSrvRecords)
                {
                    ProtocolHandler.MinecraftServiceLookup(ref host, ref port);
                }

                return true;
            }

            return false;
        }
    }
}