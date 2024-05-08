﻿#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Timers;
using UnityEngine;

namespace CraftSharp.Protocol.Session
{
    /// <summary>
    /// Handle sessions caching and storage.
    /// </summary>
    public static class SessionCache
    {
        private const string SessionCacheFilePlaintext = "SessionCache.ini";
        private const string SessionCacheFileSerialized = "SessionCache.db";
        private static readonly string SessionCacheFileMinecraft = String.Concat(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Path.DirectorySeparatorChar,
            ".minecraft",
            Path.DirectorySeparatorChar,
            "launcher_profiles.json"
        );

        private static FileMonitor? cachemonitor;
        private static Dictionary<string, SessionToken> sessions = new Dictionary<string, SessionToken>();
        private static Timer updatetimer = new Timer(100);
        private static List<KeyValuePair<string, SessionToken>> pendingadds = new List<KeyValuePair<string, SessionToken>>();
        private static BinaryFormatter formatter = new BinaryFormatter();

        /// <summary>
        /// Retrieve whether SessionCache contains a session for the given login.
        /// </summary>
        /// <param name="login">User login used with Minecraft.net</param>
        /// <returns>TRUE if session is available</returns>
        public static bool Contains(string login)
        {
            return sessions.ContainsKey(login);
        }

        /// <summary>
        /// Store a session and save it to disk if required.
        /// </summary>
        /// <param name="login">User login used with Minecraft.net</param>
        /// <param name="session">User session token used with Minecraft.net</param>
        public static void Store(string login, SessionToken session)
        {
            if (Contains(login))
            {
                sessions[login] = session;
            }
            else
            {
                sessions.Add(login, session);
            }

            if (CornGlobal.SessionCaching == CacheType.Disk && updatetimer.Enabled == true)
            {
                pendingadds.Add(new KeyValuePair<string, SessionToken>(login, session));
            }
            else if (CornGlobal.SessionCaching == CacheType.Disk)
            {
                SaveToDisk();
            }
        }

        /// <summary>
        /// Retrieve a session token for the given login.
        /// </summary>
        /// <param name="login">User login used with Minecraft.net</param>
        /// <returns>SessionToken for given login</returns>
        public static SessionToken Get(string login)
        {
            return sessions[login];
        }

        public static string[] GetCachedLogins()
        {
            return sessions.Keys.ToArray();
        }

        /// <summary>
        /// Initialize cache monitoring to keep cache updated with external changes.
        /// </summary>
        /// <returns>TRUE if session tokens are seeded from file</returns>
        public static bool InitializeDiskCache()
        {
            cachemonitor = new FileMonitor(AppDomain.CurrentDomain.BaseDirectory, SessionCacheFilePlaintext, new FileSystemEventHandler(OnChanged));
            updatetimer.Elapsed += HandlePending;
            return LoadFromDisk();
        }

        /// <summary>
        /// Reloads cache on external cache file change.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event data</param>
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            updatetimer.Stop();
            updatetimer.Start();
        }

        /// <summary>
        /// Called after timer elapsed. Reads disk cache and adds new/modified sessions back.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event data</param>
        private static void HandlePending(object sender, ElapsedEventArgs e)
        {
            updatetimer.Stop();
            LoadFromDisk();

            foreach(KeyValuePair<string, SessionToken> pending in pendingadds.ToArray())
            {
                Store(pending.Key, pending.Value);
                pendingadds.Remove(pending);
            }
        }

        /// <summary>
        /// Reads cache file and loads SessionTokens into SessionCache.
        /// </summary>
        /// <returns>True if data is successfully loaded</returns>
        private static bool LoadFromDisk()
        {
            //Grab sessions in the Minecraft directory
            if (File.Exists(SessionCacheFileMinecraft))
            {
                if (CornGlobal.DebugMode) Debug.Log(Translations.Get("cache.loading", Path.GetFileName(SessionCacheFileMinecraft)));
                Json.JSONData mcSession = new Json.JSONData(Json.JSONData.DataType.String);
                try
                {
                    mcSession = Json.ParseJson(File.ReadAllText(SessionCacheFileMinecraft));
                }
                catch (IOException) { /* Failed to read file from disk -- ignoring */ }
                if (mcSession.Type == Json.JSONData.DataType.Object
                    && mcSession.Properties.ContainsKey("clientToken")
                    && mcSession.Properties.ContainsKey("authenticationDatabase"))
                {
                    Guid temp;
                    string clientID = mcSession.Properties["clientToken"].StringValue.Replace("-", "");
                    Dictionary<string, Json.JSONData> sessionItems = mcSession.Properties["authenticationDatabase"].Properties;
                    foreach (string key in sessionItems.Keys)
                    {
                        if (Guid.TryParseExact(key, "N", out temp))
                        {
                            Dictionary<string, Json.JSONData> sessionItem = sessionItems[key].Properties;
                            if (sessionItem.ContainsKey("displayName")
                                && sessionItem.ContainsKey("accessToken")
                                && sessionItem.ContainsKey("username")
                                && sessionItem.ContainsKey("uuid"))
                            {
                                string login = sessionItem["username"].StringValue.ToLower();
                                try
                                {
                                    SessionToken session = SessionToken.FromString(String.Join(",",
                                        sessionItem["accessToken"].StringValue,
                                        sessionItem["displayName"].StringValue,
                                        sessionItem["uuid"].StringValue.Replace("-", ""),
                                        clientID
                                    ));
                                    if (CornGlobal.DebugMode)
                                        Debug.Log(Translations.Get("cache.loaded", login, session.ID));
                                    sessions[login] = session;
                                }
                                catch (InvalidDataException) { /* Not a valid session */ }
                            }
                        }
                    }
                }
            }

            //Serialized session cache file in binary format
            if (File.Exists(SessionCacheFileSerialized))
            {
                if (CornGlobal.DebugMode)
                    Debug.Log(Translations.Get("cache.converting", SessionCacheFileSerialized));

                try
                {
                    using (FileStream fs = new FileStream(SessionCacheFileSerialized, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Dictionary<string, SessionToken> sessionsTemp = (Dictionary<string, SessionToken>)formatter.Deserialize(fs);
                        foreach (KeyValuePair<string, SessionToken> item in sessionsTemp)
                        {
                            if (CornGlobal.DebugMode)
                                Debug.Log(Translations.Get("cache.loaded", item.Key, item.Value.ID));
                            sessions[item.Key] = item.Value;
                        }
                    }
                }
                catch (IOException ex)
                {
                    Debug.LogError(Translations.Get("cache.read_fail", ex.Message));
                }
                catch (SerializationException ex2)
                {
                    Debug.LogError(Translations.Get("cache.malformed", ex2.Message));
                }
            }

            //User-editable session cache file in text format
            if (File.Exists(SessionCacheFilePlaintext))
            {
                if (CornGlobal.DebugMode)
                    Debug.Log(Translations.Get("cache.loading_session", SessionCacheFilePlaintext));

                try
                {
                    foreach (string line in FileMonitor.ReadAllLinesWithRetries(SessionCacheFilePlaintext))
                    {
                        if (!line.Trim().StartsWith("#"))
                        {
                            string[] keyValue = line.Split('=');
                            if (keyValue.Length == 2)
                            {
                                try
                                {
                                    string login = keyValue[0].ToLower();
                                    SessionToken session = SessionToken.FromString(keyValue[1]);
                                    if (CornGlobal.DebugMode)
                                        Debug.Log(Translations.Get("cache.loaded", login, session.ID));
                                    sessions[login] = session;
                                }
                                catch (InvalidDataException e)
                                {
                                    if (CornGlobal.DebugMode)
                                        Debug.Log(Translations.Get("cache.ignore_string", keyValue[1], e.Message));
                                }
                            }
                            else if (CornGlobal.DebugMode)
                            {
                                Debug.Log(Translations.Get("cache.ignore_line", line));
                            }
                        }
                    }
                }
                catch (IOException e)
                {
                    Debug.Log(Translations.Get("cache.read_fail_plain", e.Message));
                }
            }

            return sessions.Count > 0;
        }

        /// <summary>
        /// Saves SessionToken's from SessionCache into cache file.
        /// </summary>
        private static void SaveToDisk()
        {
            if (CornGlobal.DebugMode)
                Debug.Log(Translations.Get("cache.saving"));

            List<string> sessionCacheLines = new List<string>();
            sessionCacheLines.Add("# Generated by CornClient " + CornGlobal.Version + " - Edit at own risk!");
            sessionCacheLines.Add("# Login=SessionID,PlayerName,UUID,ClientID");
            foreach (KeyValuePair<string, SessionToken> entry in sessions)
                sessionCacheLines.Add(entry.Key + '=' + entry.Value.ToString());

            try
            {
                FileMonitor.WriteAllLinesWithRetries(SessionCacheFilePlaintext, sessionCacheLines);
            }
            catch (IOException e)
            {
                Debug.LogError(Translations.Get("cache.save_fail", e.Message));
            }
        }
    }
}