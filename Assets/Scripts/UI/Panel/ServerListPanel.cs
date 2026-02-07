using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using CraftSharp.Protocol;

namespace CraftSharp.UI
{
    public class ServerListPanel : MonoBehaviour
    {
        [SerializeField] private Button         closeButton;
        [SerializeField] private Button         addButton;
        [SerializeField] private Button         removeButton;
        [SerializeField] private Button         selectButton;
        [SerializeField] private CanvasGroup    canvasGroup;
        [SerializeField] private RectTransform  serverListContent;
        [SerializeField] private IconListItem   serverRecordPrefab;
        [SerializeField] private AddServerPanel addServerPanel;
        [SerializeField] private TMP_InputField serverInputField;

        private readonly List<IconListItem> items = new();
        private int selectedIndex = -1;
        private int refreshToken = 0;
        private readonly ConcurrentQueue<Action> mainThreadActions = new();
        private bool isActive = true;

        /// <summary>
        /// Hides the panel
        /// </summary>
        public void Hide()
        {
            canvasGroup.alpha = 0F;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>
        /// Shows the panel and refreshes the server list
        /// </summary>
        public void Show()
        {
            canvasGroup.alpha = 1F;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            RefreshList();
        }

        private void Start()
        {
            // Initialize server list panel as hidden
            Hide();

            // Add listeners
            closeButton.onClick.AddListener(Hide);
            addButton.onClick.AddListener(() => addServerPanel.Show());
            selectButton.onClick.AddListener(SelectServer);
            removeButton.onClick.AddListener(RemoveServer);
            addServerPanel.ServerAdded += (_) => RefreshList();
        }

        private void Update()
        {
            while (mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        private void OnDestroy()
        {
            isActive = false;
            refreshToken++;
            while (mainThreadActions.TryDequeue(out _)) { }
        }

        private void RefreshList()
        {
            Debug.Log("Refreshing server list...");

            refreshToken++;
            int token = refreshToken;
            ServerRecordManager.LoadServers();

            ClearList();

            var servers = ServerRecordManager.Servers;
            for (int i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                var item = Instantiate(serverRecordPrefab, serverListContent);
                item.ClearClickListeners();

                UpdateItemFromServer(server, item);

                // Highlight if this is the selected item
                if (i == selectedIndex)
                {
                    item.SetSelected(true);
                }

                int captured = i;
                item.AddClickListener(() =>
                {
                    // Deselect all items
                    foreach (var it in items)
                    {
                        it.SetSelected(false);
                    }

                    // Select clicked item
                    selectedIndex = captured;
                    item.SetSelected(true);
                });

                items.Add(item);

                RequestStatusAndUpdate(server, item, token);
            }
        }

        private void UpdateItemFromServer(ServerRecord server, IconListItem item)
        {
            var left = $"{server.DisplayName}\n";
            if (server.Info != null) // If we have info, show description (or server address if it's empty to keep the layout consistent)
                left += server.Info.Description == string.Empty ? $"<color=#777>{server.Address}:{server.Port}</color>" : $"<color=#777>{server.Info.Description}</color>";
            else
                left += $" ";

            string right;
            if (server.Info != null && server.Info.ProtocolVersion > 0)
            {
                var versionSupported = ProtocolHandler.IsProtocolSupported(server.Info.ProtocolVersion);
                var versionColor = versionSupported ? "#777" : "#F66";

                var latencyColor = server.Info.Latency < 150 ? "#7F7" : (server.Info.Latency < 300 ? "orange" : "#F00");

                right = $"{server.Info.PlayerCount}/{server.Info.PlayerLimit} <color={latencyColor}>{server.Info.Latency}ms</color>\n<color={versionColor}>{server.Info.VersionName} ({server.Info.ProtocolVersion})</color>";
            }
            else
                right = "Pinging...\n ";

            item.SetDescriptions(left, right);
        }

        private void RequestStatusAndUpdate(ServerRecord server, IconListItem item, int token)
        {
            Debug.Log($"Pinging server {server.DisplayName} at {server.Address}:{server.Port}...");

            _ = RequestStatusAndUpdateAsync(server, item, token);
        }

        private async Task RequestStatusAndUpdateAsync(ServerRecord server, IconListItem item, int token)
        {
            var protocolVersion = ProtocolHandler.GetMinSupported();
            var info = await Task.Run(() => ServerRecordManager.PingServer(server, protocolVersion)).ConfigureAwait(false);
            if (info != null)
            {
                Debug.Log($"Ping succeeded: {server.DisplayName} ({server.Address}:{server.Port}) in {info.Latency} ms");
            }
            else
            {
                Debug.LogWarning($"Ping failed: {server.DisplayName} ({server.Address}:{server.Port})");
            }

            if (!isActive || token != refreshToken)
                return;

            mainThreadActions.Enqueue(() =>
            {
                if (!isActive || token != refreshToken)
                    return;

                if (item == null)
                    return;

                if (info != null)
                {
                    server.Info = info;
                }
                else
                {
                    var right = $"<color=#F66>Ping failed</color>\n ";
                    item.SetDescriptions(null, right);
                    return;
                }

                UpdateItemFromServer(server, item);
            });
        }

        private void RemoveServer()
        {
            if (selectedIndex < 0 || selectedIndex >= ServerRecordManager.Servers.Count)
            {
                CornApp.Notify("No server selected", Notification.Type.Warning);
                return;
            }

            ServerRecordManager.RemoveServerAt(selectedIndex);
            selectedIndex = -1;
            RefreshList();
        }

        private void SelectServer()
        {
            if (selectedIndex < 0 || selectedIndex >= ServerRecordManager.Servers.Count)
            {
                CornApp.Notify("No server selected", Notification.Type.Warning);
                return;
            }

            var server = ServerRecordManager.Servers[selectedIndex];
            ServerRecordManager.SelectedIndex = selectedIndex;

            if (serverInputField != null)
            {
                serverInputField.text = server.Port == 25565
                    ? server.Address
                    : $"{server.Address}:{server.Port}";
            }

            Hide();
        }

        private void ClearList()
        {
            foreach (var it in items)
            {
                if (it != null)
                    Destroy(it.gameObject);
            }

            items.Clear();
        }
    }
}