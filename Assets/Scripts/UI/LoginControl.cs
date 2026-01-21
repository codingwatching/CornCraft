using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

using TMPro;

using CraftSharp.Protocol;
using CraftSharp.Protocol.ProfileKey;
using CraftSharp.Protocol.Session;

namespace CraftSharp.UI
{
    public class LoginControl : MonoBehaviour
    {
        private const string LOCALHOST_ADDRESS = "127.0.0.1";
        private const int AVATAR_SIZE = 16;

        [SerializeField] private TMP_InputField serverInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button loginCloseButton;
        [SerializeField] private Button localhostButton, manageServersButton;
        [SerializeField] private Button addProfileButton;
        [SerializeField] private Button loginPanelButton, testButton, quitButton;
        [SerializeField] private TMP_Text loadStateInfoText;
        [SerializeField] private CanvasGroup loginPanel;
        [SerializeField] private TMP_Dropdown profileDropDown;
        [SerializeField] private Sprite defaultProfileIcon;
        [SerializeField] private AuthPanel authPanel;
        [SerializeField] private AddProfilePanel addProfilePanel;
        [SerializeField] private ServerListPanel serverListPanel;
        [SerializeField] private Button enterGamePanel;
        [SerializeField] private CelestiaBridge celestiaBridge;

        #nullable enable

        private bool preparingGame = false;
        private bool resourceLoaded = false;
        private StartLoginInfo? loginInfo = null;

        #nullable disable

        private readonly Dictionary<string, UserProfile> profileOptionsByText = new();
        private readonly Dictionary<string, Sprite> avatarSpritesByName = new();

        /// <summary>
        /// Load server information in ServerIP and ServerPort variables from a "serverip:port" couple or server alias
        /// </summary>
        /// <returns>True if the server IP was valid and loaded, false otherwise</returns>
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
                // IPv4 addresses or domain names contain at least a dot
                if (sip.Length == 1 && host.Contains('.') && host.Any(char.IsLetter) && ProtocolSettings.ResolveSrvRecords)
                {
                    // Domain name without port may need Minecraft SRV Record lookup
                    ProtocolHandler.MinecraftServiceLookup(ref host, ref port);
                }

                return true;
            }

            return false;
        }

        private void TryConnectDummyServer()
        {
            if (preparingGame)
            {
                CornApp.Notify(Translations.Get("login.logging_in"), Notification.Type.Warning);
                return;
            }
            preparingGame = true;

            var serverVersionName = ProtocolHandler.ProtocolVersion2MCVer(CornClientOffline.DUMMY_PROTOCOL_VERSION);
            var protocolVersion = CornClientOffline.DUMMY_PROTOCOL_VERSION;
            CornApp.Notify(Translations.Get("mcc.server_protocol", serverVersionName, protocolVersion));

            var session = new SessionToken { PlayerName = CornClientOffline.DUMMY_USERNAME };
            var accountLower = CornClientOffline.DUMMY_USERNAME.ToLower();
            // Dummy authentication completed, hide the panel...
            HideLoginPanel();
            // Store current login info
            loginInfo = new StartLoginInfo(false, session, null, "<local>", 0,
                    protocolVersion, accountLower, string.Empty);
            StartCoroutine(StoreLoginInfoAndLoadResource(loginInfo));
        }

        private void TryConnectServer()
        {
            if (preparingGame)
            {
                CornApp.Notify(Translations.Get("login.logging_in"), Notification.Type.Warning);
                return;
            }
            preparingGame = true;

            StartCoroutine(ConnectServer());
        }

        private IEnumerator ConnectServer()
        {
            loginInfo = null;

            string serverText = serverInput.text;
            var selectedProfile = UserProfileManager.GetSelectedProfile();
            if (selectedProfile == null)
            {
                CornApp.Notify("No user profile selected.", Notification.Type.Warning);
                preparingGame = false;
                loadStateInfoText.text = Translations.Get("login.login_failed");
                yield break;
            }

            string account = selectedProfile.LoginName?.Trim() ?? string.Empty;
            string accountLower = account.ToLowerInvariant();

            var session = new SessionToken();
            #nullable enable
            PlayerKeyPair? playerKeyPair = null;
            #nullable disable

            var result = ProtocolHandler.LoginResult.LoginRequired;
            var microsoftLogin = selectedProfile.Type == UserProfileType.Microsoft;

            // Login Microsoft/Offline player account
            if (!microsoftLogin)
            {
                if (!PlayerInfo.IsValidName(account))
                {
                    CornApp.Notify(Translations.Get("login.offline_username_invalid"), Notification.Type.Warning);
                    preparingGame = false;
                    loadStateInfoText.text = Translations.Get("login.login_failed");
                    yield break;
                }

                // Enter offline mode
                CornApp.Notify(Translations.Get("mcc.offline"));
                result = ProtocolHandler.LoginResult.Success;
                session.PlayerId = "0";
                session.PlayerName = account;
            }
            else
            {
                // Validate cached session or login new session.
                var authUrl = string.Empty;
                result = UserProfileManager.StartMicrosoftAuthentication(selectedProfile, out session, out account, out authUrl);
                if (result == ProtocolHandler.LoginResult.LoginRequired)
                {
                    Debug.Log(Translations.Get("mcc.connecting", "Microsoft"));

                    // Start brower and open the page...
                    Protocol.Microsoft.OpenBrowser(authUrl);

                    // Show the browser auth panel...
                    authPanel.Show(authUrl);

                    // Wait for the user to proceed or cancel
                    while (authPanel.IsAuthenticating)
                        yield return null;

                    if (authPanel.WasAuthCancelled) // Authentication cancelled by user
                        result = ProtocolHandler.LoginResult.UserCancel;
                    else // Proceed authentication...
                    {
                        var code = authPanel.GetAuthCode();
                        result = UserProfileManager.CompleteMicrosoftAuthentication(selectedProfile, code, out session, out account);
                    }
                }
                else if (result == ProtocolHandler.LoginResult.Success)
                {
                    Debug.Log(Translations.Get("mcc.session_valid", session.PlayerName));
                }
                else
                {
                    Debug.Log(Translations.Get("mcc.session_invalid"));
                }
            }

            // Proceed to target server
            if (result == ProtocolHandler.LoginResult.Success)
            {
                accountLower = account.ToLowerInvariant();
                if (!ParseServerIP(serverText, out var host, out var port) || host is null)
                {
                    CornApp.Notify(Translations.Get("login.server_name_invalid"), Notification.Type.Warning);
                    preparingGame = false;
                    loadStateInfoText.text = Translations.Get("login.login_failed");
                    yield break;
                }

                if (ProtocolSettings.SessionCaching != ProtocolSettings.CacheType.None && session is not null)
                    SessionCache.Store(accountLower, session);

                if (microsoftLogin && ProtocolSettings.LoginWithSecureProfile && session is not null)
                {
                    // Load cached profile key from disk if necessary
                    if (ProtocolSettings.ProfileKeyCaching == ProtocolSettings.CacheType.Disk)
                    {
                        var cacheKeyLoaded = KeysCache.InitializeDiskCache();
                        if (ProtocolSettings.DebugMode)
                            Debug.Log(Translations.Get(cacheKeyLoaded ? "debug.keys_cache_ok" : "debug.keys_cache_fail"));
                    }

                    if (ProtocolSettings.ProfileKeyCaching != ProtocolSettings.CacheType.None && KeysCache.Contains(accountLower))
                    {
                        playerKeyPair = KeysCache.Get(accountLower);
                        Debug.Log(playerKeyPair.NeedRefresh()
                            ? Translations.Get("mcc.profile_key_invalid")
                            : Translations.Get("mcc.profile_key_valid", session.PlayerName));
                    }

                    if (playerKeyPair == null || playerKeyPair.NeedRefresh())
                    {
                        Debug.Log(Translations.Get("mcc.fetching_key"));
                        playerKeyPair = KeyUtils.GetNewProfileKeys(session.Id);
                        if (ProtocolSettings.ProfileKeyCaching != ProtocolSettings.CacheType.None && playerKeyPair != null)
                        {
                            KeysCache.Store(accountLower, playerKeyPair);
                        }
                    }
                }

                if (ProtocolSettings.DebugMode && session is not null)
                    Debug.Log(Translations.Get("debug.session_id", session.Id));

                // Get server version
                Debug.Log(Translations.Get("mcc.retrieve")); // Retrieve server information
                loadStateInfoText.text = Translations.Get("mcc.retrieve");
                int protocolVersion = 0;
                string receivedVersionName = string.Empty;

                bool pingResult = false;
                var pingTask = Task.Run(() => {
                    // ReSharper disable once AccessToModifiedClosure
                    pingResult = ProtocolHandler.GetServerInfo(host, port, ref receivedVersionName, ref protocolVersion);
                });

                while (!pingTask.IsCompleted) yield return null;

                if (!pingResult)
                {
                    CornApp.Notify(Translations.Get("error.ping"), Notification.Type.Error);
                    preparingGame = false;
                    loadStateInfoText.text = Translations.Get("login.login_failed");
                    yield break;
                }
                
                CornApp.Notify(Translations.Get("mcc.server_protocol", receivedVersionName, protocolVersion));

                if (protocolVersion != 0 && session is not null) // Load corresponding data
                {
                    if (ProtocolHandler.IsProtocolSupported(protocolVersion))
                    {
                        // Authentication completed, hide the panel...
                        HideLoginPanel();
                        // Store current login info
                        loginInfo = new StartLoginInfo(true, session, playerKeyPair, host, port,
                                protocolVersion, accountLower, receivedVersionName);
                        // No need to yield return this coroutine because it's the last step here
                        StartCoroutine(StoreLoginInfoAndLoadResource(loginInfo));
                    }
                    else
                    {
                        int minSupported = ProtocolHandler.GetMinSupported();
                        int maxSupported = ProtocolHandler.GetMaxSupported();

                        if (protocolVersion > maxSupported || protocolVersion < minSupported)
                        {
                            // This version is not directly supported, yet might
                            // still be joinable if ViaBackwards' installed

                            protocolVersion = protocolVersion > maxSupported ? maxSupported : minSupported; // Try our luck

                            // Authentication completed, hide the panel...
                            HideLoginPanel();
                            
                            var altVersionName = ProtocolHandler.ProtocolVersion2MCVer(protocolVersion);
                            
                            // Store current login info
                            loginInfo = new StartLoginInfo(true, session, playerKeyPair, host, port,
                                    protocolVersion, accountLower, receivedVersionName + $" {altVersionName} (using v{protocolVersion})");
                            // Display a notification
                            CornApp.Notify($"Using alternative version {altVersionName} (protocol v{protocolVersion})", Notification.Type.Warning);
                            // No need to yield return this coroutine because it's the last step here
                            StartCoroutine(StoreLoginInfoAndLoadResource(loginInfo));
                        }
                        else
                        {
                            CornApp.Notify(Translations.Get("error.unsupported"), Notification.Type.Error);
                            preparingGame = false;
                        }
                    }
                }
                else // Unable to determine server version
                {
                    CornApp.Notify(Translations.Get("error.determine"), Notification.Type.Error);
                    preparingGame = false;
                }
            }
            else
            {
                var failureMessage = Translations.Get("error.login");
                var failureReason = result switch
                {
                    ProtocolHandler.LoginResult.AccountMigrated      => "error.login.migrated",
                    ProtocolHandler.LoginResult.ServiceUnavailable   => "error.login.server",
                    ProtocolHandler.LoginResult.WrongPassword        => "error.login.blocked",
                    ProtocolHandler.LoginResult.InvalidResponse      => "error.login.response",
                    ProtocolHandler.LoginResult.NotPremium           => "error.login.premium",
                    ProtocolHandler.LoginResult.OtherError           => "error.login.network",
                    ProtocolHandler.LoginResult.SSLError             => "error.login.ssl",
                    ProtocolHandler.LoginResult.UserCancel           => "error.login.cancel",
                    _                                                => "error.login.unknown"
                };
                failureMessage += Translations.Get(failureReason);
                loadStateInfoText.text = Translations.Get("login.login_failed");
                CornApp.Notify(failureMessage, Notification.Type.Error);

                if (result == ProtocolHandler.LoginResult.SSLError)
                    CornApp.Notify(Translations.Get("error.login.ssl_help"), Notification.Type.Error);
                
                Debug.LogError(failureMessage);

                preparingGame = false;
            }
        }

        private IEnumerator StoreLoginInfoAndLoadResource(StartLoginInfo info)
        {
            loginInfo = info;

            var resLoadFlag = new DataLoadFlag();
            yield return StartCoroutine(CornApp.Instance.PrepareDataAndResource(info.ProtocolVersion,
                resLoadFlag, (status, progress) => loadStateInfoText.text = Translations.Get(status) + progress));
            
            if (resLoadFlag.Failed)
            {
                resourceLoaded = false;
                preparingGame = false;

                Debug.LogWarning("Resource load failed");

                // Show login panel again
                ShowLoginPanel();
            }
            else
            {
                resourceLoaded = true;
                yield return StartCoroutine(celestiaBridge.StopAndMakePortal(() =>
                {
                    // Set enter game panel to active
                    enterGamePanel.gameObject.SetActive(true);
                    loadStateInfoText.text = Translations.Get("login.click_to_enter");
                }));
            }
        }

        private void ShowLoginPanel()
        {   // Show login panel and hide button
            loginPanel.alpha = 1F;
            loginPanel.blocksRaycasts = true;
            loginPanel.interactable = true;
            loginPanelButton.gameObject.SetActive(false);
            loginPanelButton.interactable = false;
        }

        private void HideLoginPanel()
        {   // Hide login panel and show button
            loginPanel.alpha = 0F;
            loginPanel.blocksRaycasts = false;
            loginPanel.interactable = false;
            loginPanelButton.gameObject.SetActive(true);
            loginPanelButton.interactable = true;
        }

        private void ShowAddProfilePanel()
        {
            addProfilePanel.Show();
        }

        private void UpdateStoredUserProfiles()
        {
            profileDropDown.ClearOptions();
            profileOptionsByText.Clear();

            UserProfileManager.LoadProfiles();

            foreach (var profile in UserProfileManager.Profiles)
            {
                var optionText = profile.LoginName;
                var avatarName = GetAvatarName(profile);
                if (profile.Type == UserProfileType.Offline)
                {
                    optionText += $" ({Translations.Get("login.offline")})";
                }

                var optionData = new TMP_Dropdown.OptionData(optionText, defaultProfileIcon, Color.white);
                profileDropDown.options.Add(optionData);
                profileOptionsByText[optionText] = profile;
                if (!string.IsNullOrWhiteSpace(avatarName))
                {
                    StartCoroutine(LoadAvatarForOption(optionData, avatarName));
                }
            }

            profileDropDown.interactable = UserProfileManager.Profiles.Count > 0;
            loginButton.interactable = UserProfileManager.Profiles.Count > 0;

            if (UserProfileManager.Profiles.Count == 0)
            {
                profileDropDown.options.Add(new TMP_Dropdown.OptionData("No profiles", defaultProfileIcon, Color.white));
                profileDropDown.value = 0;
            }
            else
            {
                profileDropDown.value = Mathf.Clamp(UserProfileManager.SelectedIndex, 0, UserProfileManager.Profiles.Count - 1);
            }

            profileDropDown.RefreshShownValue();
        }

        private static string GetAvatarName(UserProfile profile)
        {
            var name = string.IsNullOrWhiteSpace(profile.PlayerName)
                ? profile.LoginName?.Trim()
                : profile.PlayerName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return PlayerInfo.IsValidName(name) ? name : string.Empty;
        }

        private static string GetAvatarCacheDirectory()
        {
            return Path.Combine(PathHelper.GetRootDirectory(), "Cached", "avatars");
        }

        private static string GetAvatarCachePath(string avatarName)
        {
            var safeName = SanitizeFileName(avatarName);
            return Path.Combine(GetAvatarCacheDirectory(), $"{safeName}.png");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "player";
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(invalidChars.Contains(c) ? '_' : c);
            }
            return builder.Length == 0 ? "player" : builder.ToString();
        }

        private IEnumerator LoadAvatarForOption(TMP_Dropdown.OptionData option, string avatarName)
        {
            var avatarKey = avatarName;
            if (avatarSpritesByName.TryGetValue(avatarKey, out var cachedSprite) && cachedSprite != null)
            {
                option.image = cachedSprite;
                profileDropDown.RefreshShownValue();
                yield break;
            }

            var cacheDir = GetAvatarCacheDirectory();
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            var cachePath = GetAvatarCachePath(avatarName);
            if (File.Exists(cachePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(cachePath);
                    if (bytes.Length > 0)
                    {
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (texture.LoadImage(bytes))
                        {
                            texture.filterMode = FilterMode.Point;
                            var sprite = Sprite.Create(texture, new Rect(0F, 0F, texture.width, texture.height), new Vector2(0.5F, 0.5F));
                            avatarSpritesByName[avatarKey] = sprite;
                            option.image = sprite;
                            profileDropDown.RefreshShownValue();
                            yield break;
                        }
                        Destroy(texture);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load cached avatar '{avatarName}': {e.Message}");
                }
            }

            var avatarUrl = $"https://minotar.net/avatar/{avatarName}/{AVATAR_SIZE}";
            using (var request = UnityWebRequestTexture.GetTexture(avatarUrl))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Failed to download avatar '{avatarName}': {request.error}");
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                    yield break;
                texture.filterMode = FilterMode.Point;

                var sprite = Sprite.Create(texture, new Rect(0F, 0F, texture.width, texture.height), new Vector2(0.5F, 0.5F));
                avatarSpritesByName[avatarKey] = sprite;
                option.image = sprite;
                profileDropDown.RefreshShownValue();

                try
                {
                    var bytes = texture.EncodeToPNG();
                    if (bytes != null && bytes.Length > 0)
                        File.WriteAllBytes(cachePath, bytes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to cache avatar '{avatarName}': {e.Message}");
                }
            }
        }

        private void RemoveProfile(UserProfileType type, string loginName)
        {
            if (!UserProfileManager.RemoveProfile(type, loginName))
                return;

            profileDropDown.Hide();
            UpdateStoredUserProfiles();
        }

        private IEnumerator SetupProfileRemoveButtons()
        {
            yield return null;

            var dropdownList = profileDropDown.transform.Find("Dropdown List");
            if (dropdownList == null)
                yield break;

            var content = dropdownList.Find("Viewport/Content");
            if (content == null)
                yield break;

            for (int i = 0; i < content.childCount; i++)
            {
                var tracker = content.GetChild(i).GetComponent<DropdownItemTracker>();
                if (tracker == null)
                    continue;

                if (tracker.RemoveButton == null)
                    tracker.RemoveButton = tracker.GetComponentInChildren<Button>(true);

                if (tracker.RemoveButton == null)
                    continue;

                var labelTransform = tracker.transform.Find("Item Label");
                var label = labelTransform != null
                    ? labelTransform.GetComponent<TMP_Text>()
                    : tracker.GetComponentInChildren<TMP_Text>(true);

                if (label == null || string.IsNullOrWhiteSpace(label.text))
                    continue;

                if (!profileOptionsByText.TryGetValue(label.text, out var profile))
                    continue;

                tracker.ProfileType = profile.Type;
                tracker.LoginName = profile.LoginName;
                tracker.RemoveButton.onClick.RemoveAllListeners();
                tracker.RemoveButton.onClick.AddListener(() => RemoveProfile(tracker.ProfileType, tracker.LoginName));
            }
        }

        private void SetupProfileDropdownTriggers()
        {
            var trigger = profileDropDown.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = profileDropDown.gameObject.AddComponent<EventTrigger>();

            var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener(_ => StartCoroutine(SetupProfileRemoveButtons()));
            trigger.triggers.Add(clickEntry);

            var submitEntry = new EventTrigger.Entry { eventID = EventTriggerType.Submit };
            submitEntry.callback.AddListener(_ => StartCoroutine(SetupProfileRemoveButtons()));
            trigger.triggers.Add(submitEntry);
        }
        
        private IEnumerator UpdateSelectedProfile(int selection)
        {
            // Workaround for UGUI EventSystem click event bug, if the option being clicked is above the login button,
            // it'll trigger a null object exception every frame until the mouse pointer is moved out from the button area
            loginButton.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.2F);
            loginButton.gameObject.SetActive(true);

            if (UserProfileManager.Profiles.Count > 0)
            {
                UserProfileManager.SelectedIndex = selection;
            }
        }

        private IEnumerator EnterGame()
        {
            if (resourceLoaded && loginInfo is not null)
            {
                enterGamePanel.gameObject.SetActive(false); // Disable this panel after click

                celestiaBridge.EnterPortal();

                yield return new WaitForSecondsRealtime(2F);

                // We cannot directly use StartCoroutine to call StartLogin here, which will stop running when
                // this scene is unloaded and LoginControl object is destroyed
                CornApp.Instance.StartLoginCoroutine(loginInfo, _ => preparingGame = false,
                        status => loadStateInfoText.text = Translations.Get(status));
            }
        }

        private static void QuitGame()
        {
            Application.Quit();
        }

        private void Start()
        {
            // Generate default data or check update (Need to be done with priority because it contains translation texts)
            var extraDataDir = PathHelper.GetExtraDataDirectory();
            var builtinResLoad = BuiltinResourceHelper.ReadyBuiltinResource(
                    CornApp.CORN_CRAFT_BUILTIN_FILE_NAME, CornApp.CORN_CRAFT_BUILTIN_VERSION, extraDataDir,
                    _ => { }, () => { }, succeeded =>
                    {
                        // Reload translations after generating builtin asset files
                        if (succeeded) Translations.LoadTranslationsFile(ProtocolSettings.Language);
                    });
            
            while (builtinResLoad.MoveNext()) { /* Do nothing */ }
            
            // Initialize controls
            enterGamePanel.gameObject.SetActive(false);
            enterGamePanel.onClick.AddListener(() => StartCoroutine(EnterGame()));
            profileDropDown.onValueChanged.AddListener(selection => StartCoroutine(UpdateSelectedProfile(selection)));
            SetupProfileDropdownTriggers();

            //Load cached sessions from disk if necessary
            if (ProtocolSettings.SessionCaching == ProtocolSettings.CacheType.Disk)
            {
                var cacheLoaded = SessionCache.InitializeDiskCache();
                if (ProtocolSettings.DebugMode)
                    Debug.Log(Translations.Get(cacheLoaded ? "debug.session_cache_ok" : "debug.session_cache_fail"));
            }

            // TODO: Also initialize server with cached values
            serverInput.text = LOCALHOST_ADDRESS;
            UpdateStoredUserProfiles();

            // Prepare panels at start
            ShowLoginPanel();

            // Add listeners
            addProfileButton.onClick.AddListener(ShowAddProfilePanel);
            addProfilePanel.ProfileAdded += _ => UpdateStoredUserProfiles();
            localhostButton.onClick.AddListener(() => serverInput.text = LOCALHOST_ADDRESS);

            testButton.onClick.AddListener(TryConnectDummyServer);
            quitButton.onClick.AddListener(QuitGame);

            loginButton.onClick.AddListener(TryConnectServer);

            loginCloseButton.onClick.AddListener(HideLoginPanel);
            loginPanelButton.onClick.AddListener(ShowLoginPanel);

            // Used for testing MC format code parsing
            // loadStateInfoText.text = StringConvert.MC2TMP("Hello world §a[§a§a-1, §a1 §6[Bl§b[HHH]ah] Hello §c[Color RE§rD]  §a1§r] (blah)");
            loadStateInfoText.text = $"CornCraft {ProtocolSettings.Version} Powered by <u>Minecraft Console Client</u>";

            // Release cursor (Useful when re-entering login scene from game)
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
