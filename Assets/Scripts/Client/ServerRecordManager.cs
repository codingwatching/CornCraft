#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using CraftSharp.Protocol.Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CraftSharp
{
    /// <summary>
    /// Server information retrieved from server list ping
    /// </summary>
    [Serializable]
    public class ServerInfo
    {
        /// <summary>
        /// Server version name (e.g., "1.20.1")
        /// </summary>
        public string VersionName = string.Empty;

        /// <summary>
        /// Server protocol version number
        /// </summary>
        public int ProtocolVersion = 0;

        /// <summary>
        /// Current player count online
        /// </summary>
        public int PlayerCount = 0;

        /// <summary>
        /// Maximum allowed players
        /// </summary>
        public int PlayerLimit = 0;

        /// <summary>
        /// Server description/MOTD
        /// </summary>
        public string Description = string.Empty;

        /// <summary>
        /// Base64-encoded favicon PNG data (with data:image/png;base64, prefix)
        /// </summary>
        public string Favicon = string.Empty;

        /// <summary>
        /// Whether the server enforces secure chat
        /// </summary>
        public bool EnforcesSecureChat = false;

        /// <summary>
        /// Whether the server prevents chat reports
        /// </summary>
        public bool PreventsChatReports = false;

        /// <summary>
        /// Server latency in milliseconds
        /// </summary>
        public long Latency = 0;

        /// <summary>
        /// Timestamp when this info was last updated
        /// </summary>
        public DateTime LastUpdated = DateTime.MinValue;
    }

    /// <summary>
    /// Server record for connection - stores basic connection information
    /// </summary>
    [Serializable]
    public class ServerRecord
    {
        /// <summary>
        /// Display name for the server
        /// </summary>
        public string DisplayName = string.Empty;

        /// <summary>
        /// Server address (IP or domain)
        /// </summary>
        public string Address = string.Empty;

        /// <summary>
        /// Server port (default 25565)
        /// </summary>
        public ushort Port = 25565;

        /// <summary>
        /// Server info (version, players, description, favicon, etc.)
        /// </summary>
        [JsonIgnore]
        public ServerInfo Info = new();
    }

    /// <summary>
    /// Data class for serialization of server records
    /// </summary>
    [Serializable]
    public class ServerRecordData
    {
        public int SelectedIndex = 0;
        public List<ServerRecord> Servers = new();
    }

    /// <summary>
    /// Manager for server records - handles adding, removing, and managing servers
    /// </summary>
    public static class ServerRecordManager
    {
        private const string ServersFileName = "ServerRecords.json";
        private static readonly List<ServerRecord> servers = new();
        private static bool loaded;
        private static int selectedIndex;

        public static IReadOnlyList<ServerRecord> Servers => servers;

        public static int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = ClampSelectedIndex(value);
                SaveServers();
            }
        }

        public static ServerRecord? SelectedServer =>
            selectedIndex >= 0 && selectedIndex < servers.Count ? servers[selectedIndex] : null;

        /// <summary>
        /// Load servers from storage
        /// </summary>
        public static void LoadServers()
        {
            if (loaded)
                return;

            loaded = true;
            servers.Clear();

            var path = GetServersPath();
            if (File.Exists(path))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<ServerRecordData>(File.ReadAllText(path));
                    if (data?.Servers != null)
                    {
                        servers.AddRange(FilterDuplicates(data.Servers));
                        selectedIndex = ClampSelectedIndex(data.SelectedIndex);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read server records: {e.Message}");
                }
            }

            selectedIndex = ClampSelectedIndex(selectedIndex);
        }

        /// <summary>
        /// Get the currently selected server
        /// </summary>
        public static ServerRecord? GetSelectedServer()
        {
            return SelectedServer;
        }

        /// <summary>
        /// Add or update a server record
        /// </summary>
        public static void AddOrUpdateServer(ServerRecord server)
        {
            var index = FindServerIndex(server.Address, server.Port);
            if (index >= 0)
            {
                servers[index] = server;
            }
            else
            {
                servers.Add(server);
            }

            selectedIndex = ClampSelectedIndex(selectedIndex);
            SaveServers();
        }

        /// <summary>
        /// Remove a server at the specified index
        /// </summary>
        public static bool RemoveServerAt(int index)
        {
            if (index < 0 || index >= servers.Count)
                return false;

            servers.RemoveAt(index);

            if (selectedIndex > index)
                selectedIndex--;
            else if (selectedIndex == index)
                selectedIndex = index;

            selectedIndex = ClampSelectedIndex(selectedIndex);
            SaveServers();
            return true;
        }

        /// <summary>
        /// Remove a server by address and port
        /// </summary>
        public static bool RemoveServer(string address, ushort port)
        {
            var index = FindServerIndex(address, port);
            if (index < 0)
                return false;

            return RemoveServerAt(index);
        }

        /// <summary>
        /// Ping a server to get its information
        /// </summary>
        /// <param name="server">Server record to ping</param>
        /// <param name="protocolVersion">Protocol version to use</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns>Server info or null if ping failed</returns>
        public static ServerInfo? PingServer(ServerRecord server, int protocolVersion, int timeout = 5000)
        {
            try
            {
                Debug.Log($"[ServerPing] Start {server.DisplayName} ({server.Address}:{server.Port}) timeout={timeout}ms");
                var serverInfo = new ServerInfo();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using (var client = new TcpClient())
                {
                    Debug.Log($"[ServerPing] Connecting {server.Address}:{server.Port}");
                    if (!TryConnectWithTimeout(client, server.Address, server.Port, timeout))
                    {
                        Debug.LogWarning($"[ServerPing] Connect failed {server.Address}:{server.Port}");
                        return null;
                    }

                    client.ReceiveTimeout = timeout;
                    client.SendTimeout = timeout;

                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = timeout;
                        stream.WriteTimeout = timeout;
                        var readCache = new Queue<byte>();

                        // Send handshake packet (Handshake -> Status -> Ping)
                        Debug.Log($"[ServerPing] Sending handshake {server.Address}:{server.Port}");
                        if (!SendHandshakePacket(client, server.Address, protocolVersion, server.Port, timeout))
                        {
                            Debug.LogWarning($"[ServerPing] Handshake failed {server.Address}:{server.Port}");
                            return null;
                        }

                        Debug.Log($"[ServerPing] Sending status request {server.Address}:{server.Port}");
                        if (!SendStatusRequestPacket(client, timeout))
                        {
                            Debug.LogWarning($"[ServerPing] Status request failed {server.Address}:{server.Port}");
                            return null;
                        }

                        // Receive status response
                        Debug.Log($"[ServerPing] Waiting status response {server.Address}:{server.Port}");
                        var statusResponse = ReceiveStatusResponse(client, stream, readCache, timeout);
                        if (statusResponse == null)
                        {
                            Debug.LogWarning($"[ServerPing] Status response missing {server.Address}:{server.Port}");
                            return null;
                        }

                        // Parse the JSON response
                        Debug.Log($"[ServerPing] Parsing status response {server.Address}:{server.Port}");
                        ParseStatusResponse(statusResponse, serverInfo);

                        // Send ping request
                        var pingPayload = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(pingPayload);

                        Debug.Log($"[ServerPing] Sending ping {server.Address}:{server.Port}");
                        if (!SendPingRequestPacket(client, pingPayload, timeout))
                        {
                            Debug.LogWarning($"[ServerPing] Ping request failed {server.Address}:{server.Port}");
                            return null;
                        }

                        // Receive pong response
                        Debug.Log($"[ServerPing] Waiting pong {server.Address}:{server.Port}");
                        if (!ReceivePongResponse(client, stream, readCache, pingPayload, timeout))
                        {
                            Debug.LogWarning($"[ServerPing] Pong missing {server.Address}:{server.Port}");
                            return null;
                        }

                        stopwatch.Stop();
                        serverInfo.Latency = stopwatch.ElapsedMilliseconds;
                    }
                }

                serverInfo.LastUpdated = DateTime.UtcNow;
                Debug.Log($"[ServerPing] Success {server.Address}:{server.Port} latency={serverInfo.Latency}ms");
                return serverInfo;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Error {server.Address}:{server.Port}: {e.Message}");
                return null;
            }
        }

        private static bool SendHandshakePacket(TcpClient client, string address, int protocolVersion, ushort port, int timeout)
        {
            try
            {
                Debug.Log($"[ServerPing] Build handshake packet {address}:{port}");
                var packet = new List<byte>();

                // Packet ID: 0x00 (Handshake)
                packet.Add(0x00);

                // Protocol Version: use a valid value for status ping
                packet.AddRange(DataTypes.GetVarInt(protocolVersion));

                // Server Address
                var addressBytes = Encoding.UTF8.GetBytes(address);
                packet.AddRange(DataTypes.GetVarInt(addressBytes.Length));
                packet.AddRange(addressBytes);

                // Server Port
                packet.AddRange(BitConverter.GetBytes(port));
                if (BitConverter.IsLittleEndian)
                    packet.Reverse(packet.Count - 2, 2);

                // Next State: 1 (Status)
                packet.AddRange(DataTypes.GetVarInt(1));

                // Write packet length prefix and packet data
                var packetLength = DataTypes.GetVarInt(packet.Count);
                Debug.Log($"[ServerPing] Handshake write length={packetLength.Length} data={packet.Count}");
                Debug.Log($"[ServerPing] Handshake write length bytes");
                if (!WriteExact(client, packetLength, 0, packetLength.Length, timeout))
                    return false;
                Debug.Log($"[ServerPing] Handshake write data bytes");
                if (!WriteExact(client, packet.ToArray(), 0, packet.Count, timeout))
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Failed to send handshake packet: {e.Message}");
                return false;
            }
        }

        private static bool SendStatusRequestPacket(TcpClient client, int timeout)
        {
            try
            {
                var packet = new List<byte>();
                packet.Add(0x00); // Packet ID: Status Request

                var packetLength = DataTypes.GetVarInt(packet.Count);
                Debug.Log($"[ServerPing] Status write length={packetLength.Length} data={packet.Count}");
                if (!WriteExact(client, packetLength, 0, packetLength.Length, timeout))
                    return false;
                if (!WriteExact(client, packet.ToArray(), 0, packet.Count, timeout))
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Failed to send status request packet: {e.Message}");
                return false;
            }
        }

        private static bool SendPingRequestPacket(TcpClient client, byte[] payload, int timeout)
        {
            try
            {
                var packet = new List<byte>();
                packet.Add(0x01); // Packet ID: Ping Request
                packet.AddRange(payload);

                var packetLength = DataTypes.GetVarInt(packet.Count);
                Debug.Log($"[ServerPing] Ping write length={packetLength.Length} data={packet.Count}");
                if (!WriteExact(client, packetLength, 0, packetLength.Length, timeout))
                    return false;
                if (!WriteExact(client, packet.ToArray(), 0, packet.Count, timeout))
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Failed to send ping request packet: {e.Message}");
                return false;
            }
        }

        private static string? ReceiveStatusResponse(TcpClient client, NetworkStream stream, Queue<byte> readCache, int timeout)
        {
            try
            {
                Debug.Log($"[ServerPing] Read status packet length");
                int packetLength = ReadNextVarIntFromStream(client, stream, readCache, timeout);
                if (packetLength <= 0)
                    return null;

                var packetData = new byte[packetLength];
                Debug.Log($"[ServerPing] Read status packet bytes {packetLength}");
                if (!ReadExact(client, stream, readCache, packetData, packetLength, timeout))
                    return null;

                var packetQueue = new Queue<byte>(packetData);
                int packetId = DataTypes.ReadNextVarInt(packetQueue);
                if (packetId != 0x00)
                    return null;

                int jsonLength = DataTypes.ReadNextVarInt(packetQueue);
                var jsonBytes = DequeueBytes(packetQueue, jsonLength);
                if (jsonBytes == null)
                    return null;

                return Encoding.UTF8.GetString(jsonBytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Failed to receive status response: {e.Message}");
                return null;
            }
        }

        private static bool ReceivePongResponse(TcpClient client, NetworkStream stream, Queue<byte> readCache, byte[] expectedPayload, int timeout)
        {
            try
            {
                Debug.Log($"[ServerPing] Read pong packet length");
                int packetLength = ReadNextVarIntFromStream(client, stream, readCache, timeout);
                if (packetLength <= 0)
                    return false;

                var packetData = new byte[packetLength];
                Debug.Log($"[ServerPing] Read pong packet bytes {packetLength}");
                if (!ReadExact(client, stream, readCache, packetData, packetLength, timeout))
                    return false;

                var packetQueue = new Queue<byte>(packetData);
                int packetId = DataTypes.ReadNextVarInt(packetQueue);
                if (packetId != 0x01)
                    return false;

                var receivedPayload = DequeueBytes(packetQueue, 8);
                if (receivedPayload == null)
                    return false;

                // Note: We don't strictly check if payloads match as some servers may not properly implement this
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ServerPing] Failed to receive pong response: {e.Message}");
                return false;
            }
        }

        private static bool TryConnectWithTimeout(TcpClient client, string address, ushort port, int timeout)
        {
            try
            {
                Debug.Log($"[ServerPing] BeginConnect {address}:{port}");
                var result = client.BeginConnect(address, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(timeout);
                if (!success)
                {
                    Debug.LogWarning($"[ServerPing] Connection to {address}:{port} timed out");
                    client.Close();
                    return false;
                }

                Debug.Log($"[ServerPing] EndConnect {address}:{port}");
                client.EndConnect(result);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerPing] Failed to connect to {address}:{port}: {e.Message}");
                return false;
            }
        }

        private static int ReadNextVarIntFromStream(TcpClient client, NetworkStream stream, Queue<byte> buffer, int timeout)
        {
            Debug.Log($"[ServerPing] ReadVarInt start timeout={timeout}ms");
            int deadline = Environment.TickCount + timeout;
            while (Environment.TickCount < deadline)
            {
                if (buffer.Count > 0)
                {
                    try
                    {
                        var probe = new Queue<byte>(buffer);
                        int value = DataTypes.ReadNextVarInt(probe);
                        if (probe.Count != buffer.Count)
                        {
                            return DataTypes.ReadNextVarInt(buffer);
                        }
                    }
                    catch
                    {
                        // Not enough bytes yet, continue reading
                    }
                }

                int valueByte = ReadByteWithTimeout(client, stream, deadline);
                if (valueByte < 0)
                    return -1;

                buffer.Enqueue((byte)valueByte);

                if (buffer.Count > 5)
                    return -1;
            }

            return -1;
        }

        private static bool ReadExact(TcpClient client, NetworkStream stream, Queue<byte> cache, byte[] buffer, int length, int timeout)
        {
            int offset = 0;
            int deadline = Environment.TickCount + timeout;
            while (cache.Count > 0 && offset < length)
            {
                buffer[offset++] = cache.Dequeue();
            }
            while (offset < length)
            {
                if (!WaitForData(client, deadline))
                    return false;

                int read = stream.Read(buffer, offset, length - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }

            return true;
        }

        private static bool WriteExact(TcpClient client, byte[] buffer, int offset, int length, int timeout)
        {
            int deadline = Environment.TickCount + timeout;
            var socket = client.Client;
            int sent = 0;
            Debug.Log($"[ServerPing] WriteExact start length={length}");
            while (sent < length)
            {
                int remaining = deadline - Environment.TickCount;
                if (remaining <= 0)
                {
                    Debug.LogWarning("[ServerPing] Write timed out");
                    return false;
                }

                int pollMs = Math.Min(remaining, 50);
                if (!socket.Poll(pollMs * 1000, SelectMode.SelectWrite))
                {
                    Thread.Sleep(1);
                    continue;
                }

                int bytes = socket.Send(buffer, offset + sent, length - sent, SocketFlags.None);
                if (bytes <= 0)
                {
                    Debug.LogWarning("[ServerPing] Write failed (socket closed)");
                    return false;
                }

                sent += bytes;
            }

            return true;
        }

        private static int ReadByteWithTimeout(TcpClient client, NetworkStream stream, int deadline)
        {
            Debug.Log($"[ServerPing] ReadByteWithTimeout");
            if (!WaitForData(client, deadline))
                return -1;

            return stream.ReadByte();
        }

        private static bool WaitForData(TcpClient client, int deadline)
        {
            var socket = client.Client;
            while (Environment.TickCount < deadline)
            {
                if (socket.Available > 0)
                    return true;

                int remaining = deadline - Environment.TickCount;
                if (remaining <= 0)
                    break;

                int pollMs = Math.Min(remaining, 50);
                if (socket.Poll(pollMs * 1000, SelectMode.SelectRead))
                {
                    if (socket.Available > 0)
                        return true;

                    return false; // socket closed
                }

                Thread.Sleep(1);
            }

            return false;
        }

        private static void ParseStatusResponse(string json, ServerInfo info)
        {
            try
            {
                var obj = JObject.Parse(json);

                // Version
                if (obj.TryGetValue("version", out var versionToken) && versionToken is JObject version)
                {
                    if (version.TryGetValue("name", out var nameToken))
                        info.VersionName = nameToken.ToString();

                    if (version.TryGetValue("protocol", out var protocolToken) && int.TryParse(protocolToken.ToString(), out int protocol))
                        info.ProtocolVersion = protocol;
                }

                // Players
                if (obj.TryGetValue("players", out var playersToken) && playersToken is JObject players)
                {
                    if (players.TryGetValue("online", out var onlineToken) && int.TryParse(onlineToken.ToString(), out int online))
                        info.PlayerCount = online;

                    if (players.TryGetValue("max", out var maxToken) && int.TryParse(maxToken.ToString(), out int max))
                        info.PlayerLimit = max;
                }

                // Description
                if (obj.TryGetValue("description", out var descToken))
                {
                    if (descToken is JObject descObj && descObj.TryGetValue("text", out var textToken))
                        info.Description = textToken.ToString();
                    else if (descToken.Type == JTokenType.String)
                        info.Description = descToken.ToString();
                }

                // Favicon
                if (obj.TryGetValue("favicon", out var faviconToken))
                    info.Favicon = faviconToken.ToString();

                // Secure chat and chat reports
                if (obj.TryGetValue("enforcesSecureChat", out var secureToken) && bool.TryParse(secureToken.ToString(), out bool secure))
                    info.EnforcesSecureChat = secure;

                if (obj.TryGetValue("preventsChatReports", out var preventsToken) && bool.TryParse(preventsToken.ToString(), out bool prevents))
                    info.PreventsChatReports = prevents;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse status response: {e.Message}");
            }
        }

        private static int FindServerIndex(string address, ushort port)
        {
            if (string.IsNullOrWhiteSpace(address))
                return -1;

            return servers.FindIndex(server =>
                string.Equals(server.Address?.Trim(), address.Trim(), StringComparison.OrdinalIgnoreCase) &&
                server.Port == port);
        }

        private static List<ServerRecord> FilterDuplicates(IEnumerable<ServerRecord> source)
        {
            var result = new List<ServerRecord>();
            foreach (var server in source.Where(server => server != null))
            {
                var address = server.Address?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(address))
                    continue;

                if (result.Any(existing =>
                        string.Equals(existing.Address, address, StringComparison.OrdinalIgnoreCase) &&
                        existing.Port == server.Port))
                {
                    continue;
                }

                server.Address = address;
                server.DisplayName ??= string.Empty;
                server.Info ??= new ServerInfo();
                result.Add(server);
            }

            return result;
        }

        private static int ClampSelectedIndex(int index)
        {
            if (servers.Count == 0)
                return -1;

            if (index < 0)
                return 0;

            if (index >= servers.Count)
                return servers.Count - 1;

            return index;
        }

        private static string GetServersPath()
        {
            return Path.Combine(PathHelper.GetRootDirectory(), ServersFileName);
        }

        private static void SaveServers()
        {
            var data = new ServerRecordData
            {
                SelectedIndex = selectedIndex,
                Servers = servers
            };

            try
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetServersPath(), json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save server records: {e.Message}");
            }
        }

        private static byte[]? DequeueBytes(Queue<byte> queue, int length)
        {
            if (length < 0 || queue.Count < length)
                return null;

            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = queue.Dequeue();
            }

            return bytes;
        }
    }
}
