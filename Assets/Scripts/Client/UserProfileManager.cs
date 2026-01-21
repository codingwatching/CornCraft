#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

using CraftSharp.Protocol;
using CraftSharp.Protocol.Session;

namespace CraftSharp
{
    public enum UserProfileType
    {
        Microsoft,
        Offline
    }

    [Serializable]
    public class UserProfile
    {
        public string LoginName = string.Empty;
        public UserProfileType Type = UserProfileType.Offline;
        public string MicrosoftRefreshToken = string.Empty;
        public string PlayerName = string.Empty;
        public string PlayerUuid = string.Empty;
    }

    [Serializable]
    public class UserProfileData
    {
        public int SelectedIndex = 0;
        public List<UserProfile> Profiles = new();
    }

    public static class UserProfileManager
    {
        private const string ProfilesFileName = "UserProfiles.json";
        private static readonly List<UserProfile> profiles = new();
        private static bool loaded;
        private static int selectedIndex;

        public static IReadOnlyList<UserProfile> Profiles => profiles;

        public static int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = ClampSelectedIndex(value);
                SaveProfiles();
            }
        }

        public static UserProfile? SelectedProfile =>
            selectedIndex >= 0 && selectedIndex < profiles.Count ? profiles[selectedIndex] : null;

        public static void LoadProfiles()
        {
            if (loaded)
                return;

            loaded = true;
            profiles.Clear();

            var path = GetProfilesPath();
            if (File.Exists(path))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<UserProfileData>(File.ReadAllText(path));
                    if (data?.Profiles != null)
                    {
                        profiles.AddRange(FilterDuplicates(data.Profiles));
                        selectedIndex = ClampSelectedIndex(data.SelectedIndex);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read user profiles: {e.Message}");
                }
            }

            SeedFromSessionCache();
            selectedIndex = ClampSelectedIndex(selectedIndex);
        }

        public static UserProfile? GetSelectedProfile()
        {
            return SelectedProfile;
        }

        public static void AddOrUpdateProfile(UserProfile profile)
        {
            var index = FindProfileIndex(profile.Type, profile.LoginName);
            if (index >= 0)
            {
                profiles[index] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            selectedIndex = ClampSelectedIndex(selectedIndex);
            SaveProfiles();
        }

        public static bool RemoveProfileAt(int index)
        {
            if (index < 0 || index >= profiles.Count)
                return false;

            profiles.RemoveAt(index);

            if (selectedIndex > index)
                selectedIndex--;
            else if (selectedIndex == index)
                selectedIndex = index;

            selectedIndex = ClampSelectedIndex(selectedIndex);
            SaveProfiles();
            return true;
        }

        public static bool RemoveProfile(UserProfileType type, string loginName)
        {
            var index = FindProfileIndex(type, loginName);
            if (index < 0)
                return false;

            return RemoveProfileAt(index);
        }

        public static ProtocolHandler.LoginResult StartMicrosoftAuthentication(UserProfile profile, out SessionToken session, out string account, out string? authUrl)
        {
            session = new SessionToken();
            account = profile.LoginName?.Trim() ?? string.Empty;
            authUrl = null;

            var result = ProtocolHandler.LoginResult.LoginRequired;
            var accountLower = account.ToLowerInvariant();

            if (ProtocolSettings.SessionCaching != ProtocolSettings.CacheType.None && SessionCache.Contains(accountLower))
            {
                session = SessionCache.Get(accountLower);
                result = ProtocolHandler.GetTokenValidation(session);
                if (result != ProtocolHandler.LoginResult.Success && !string.IsNullOrWhiteSpace(session.RefreshToken))
                {
                    try
                    {
                        result = ProtocolHandler.MicrosoftLoginRefresh(session.RefreshToken, out session, ref account);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Refresh access token fail: " + e.Message);
                        result = ProtocolHandler.LoginResult.InvalidResponse;
                    }
                }
            }

            if (result != ProtocolHandler.LoginResult.Success && !string.IsNullOrWhiteSpace(profile.MicrosoftRefreshToken))
            {
                try
                {
                    result = ProtocolHandler.MicrosoftLoginRefresh(profile.MicrosoftRefreshToken, out session, ref account);
                }
                catch (Exception e)
                {
                    Debug.LogError("Refresh access token fail: " + e.Message);
                    result = ProtocolHandler.LoginResult.InvalidResponse;
                }
            }

            if (result == ProtocolHandler.LoginResult.Success)
            {
                UpdateMicrosoftProfile(profile, session, account);
                return result;
            }

            authUrl = string.IsNullOrEmpty(account)
                ? Protocol.Microsoft.SignInUrl
                : Protocol.Microsoft.GetSignInUrlWithHint(account);
            return ProtocolHandler.LoginResult.LoginRequired;
        }

        public static ProtocolHandler.LoginResult CompleteMicrosoftAuthentication(UserProfile profile, string authCode, out SessionToken session, out string account)
        {
            session = new SessionToken();
            account = profile.LoginName?.Trim() ?? string.Empty;
            ProtocolHandler.LoginResult result;

            try
            {
                result = ProtocolHandler.MicrosoftBrowserLogin(authCode, out session, ref account);
            }
            catch
            {
                result = ProtocolHandler.LoginResult.OtherError;
            }

            if (result == ProtocolHandler.LoginResult.Success)
            {
                UpdateMicrosoftProfile(profile, session, account);
            }

            return result;
        }

        private static void UpdateMicrosoftProfile(UserProfile profile, SessionToken session, string account)
        {
            profile.LoginName = account;
            profile.MicrosoftRefreshToken = session.RefreshToken ?? string.Empty;
            profile.PlayerName = session.PlayerName ?? string.Empty;
            profile.PlayerUuid = session.PlayerId ?? string.Empty;
            AddOrUpdateProfile(profile);
        }

        private static void SeedFromSessionCache()
        {
            var added = false;

            foreach (var login in SessionCache.GetCachedOnlineLogins())
            {
                var existingIndex = FindProfileIndex(UserProfileType.Microsoft, login);
                if (existingIndex >= 0)
                    continue;

                var session = SessionCache.Get(login);
                profiles.Add(new UserProfile
                {
                    LoginName = login,
                    Type = UserProfileType.Microsoft,
                    MicrosoftRefreshToken = session.RefreshToken ?? string.Empty,
                    PlayerName = session.PlayerName ?? string.Empty,
                    PlayerUuid = session.PlayerId ?? string.Empty
                });
                added = true;
            }

            foreach (var login in SessionCache.GetCachedOfflineLogins())
            {
                var existingIndex = FindProfileIndex(UserProfileType.Offline, login);
                if (existingIndex >= 0)
                    continue;

                profiles.Add(new UserProfile
                {
                    LoginName = login,
                    Type = UserProfileType.Offline,
                    MicrosoftRefreshToken = string.Empty,
                    PlayerName = string.Empty,
                    PlayerUuid = string.Empty
                });
                added = true;
            }

            if (added)
                SaveProfiles();
        }

        private static int FindProfileIndex(UserProfileType type, string loginName)
        {
            if (string.IsNullOrWhiteSpace(loginName))
                return -1;

            return profiles.FindIndex(profile =>
                profile.Type == type &&
                string.Equals(profile.LoginName?.Trim(), loginName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static List<UserProfile> FilterDuplicates(IEnumerable<UserProfile> source)
        {
            var result = new List<UserProfile>();
            foreach (var profile in source.Where(profile => profile != null))
            {
                var loginName = profile.LoginName?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(loginName))
                    continue;

                if (result.Any(existing =>
                        existing.Type == profile.Type &&
                        string.Equals(existing.LoginName, loginName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                profile.LoginName = loginName;
                profile.MicrosoftRefreshToken ??= string.Empty;
                profile.PlayerName ??= string.Empty;
                profile.PlayerUuid ??= string.Empty;
                result.Add(profile);
            }

            return result;
        }

        private static int ClampSelectedIndex(int index)
        {
            if (profiles.Count == 0)
                return -1;

            if (index < 0)
                return 0;

            if (index >= profiles.Count)
                return profiles.Count - 1;

            return index;
        }

        private static string GetProfilesPath()
        {
            return Path.Combine(PathHelper.GetRootDirectory(), ProfilesFileName);
        }

        private static void SaveProfiles()
        {
            var data = new UserProfileData
            {
                SelectedIndex = selectedIndex,
                Profiles = profiles
            };

            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetProfilesPath(), json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save user profiles: {e.Message}");
            }
        }
    }
}
