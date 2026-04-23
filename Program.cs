using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;

namespace SecureMessenger;

internal static class Program
{
    private static readonly ConcurrentDictionary<string, Peer> KnownPeers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> EndpointToUsername = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> UsernameToEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte[]> PeerPublicKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> KnownRooms = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> PendingIdentityEndpoints = new();

    private static readonly RSA LocalRsa = RSA.Create(2048);
    private static readonly MessageSigner Signer = new(LocalRsa);

    private static Server? _server;
    private static Client? _client;
    private static ConsoleUI? _ui;
    private static PeerDiscovery? _peerDiscovery;
    private static HeartbeatMonitor? _heartbeatMonitor;
    private static ReconnectionPolicy? _reconnectionPolicy;
    private static MessageHistory? _messageHistory;
    private static CancellationTokenSource? _backgroundCts;
    private static Task? _heartbeatTask;
    private static Peer? _lastOutboundPeer;
    private static string _username =
        Environment.GetEnvironmentVariable("SECURE_MESSENGER_NAME")
        ?? $"peer-{Environment.UserName}-{Guid.NewGuid().ToString()[..4]}";
    private static string? _currentRoom;
    private static int? _listeningPort;
    private static bool _isShuttingDown;

    private const string CreateRoomPrefix = "__CREATE_ROOM__";
    private const string JoinRoomPrefix = "__JOIN_ROOM__";
    private const string LeaveRoomPrefix = "__LEAVE_ROOM__";

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");
        Console.WriteLine($"Local peer name: {_username}");
        Console.WriteLine("Type /help for available commands");
        Console.WriteLine();

        _server = new Server();
        _client = new Client();
        _ui = new ConsoleUI();
        _peerDiscovery = new PeerDiscovery();
        _heartbeatMonitor = new HeartbeatMonitor();
        _messageHistory = new MessageHistory();
        _reconnectionPolicy = new ReconnectionPolicy(_client, _username);

        WireEvents();
        StartBackgroundServices();

        bool running = true;
        while (running)
        {
            _ui.PrintPrompt();
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            CommandResult result = _ui.ParseCommand(input);
            if (!result.IsCommand)
            {
                SendPlainMessage(result.Message!);
                continue;
            }

            switch (result.CommandType)
            {
                case CommandType.Help:
                    _ui.ShowHelp();
                    break;

                case CommandType.Listen:
                    HandleListen(result.Args!);
                    break;

                case CommandType.Connect:
                    await HandleConnectAsync(result.Args!);
                    break;

                case CommandType.RoomCreate:
                    HandleCreateRoom(result.Args![0]);
                    break;

                case CommandType.RoomJoin:
                    HandleJoinRoom(result.Args![0]);
                    break;

                case CommandType.RoomLeave:
                    HandleLeaveRoom(result.Args![0]);
                    break;

                case CommandType.RoomList:
                    HandleRooms();
                    break;

                case CommandType.SendMessage:
                    HandleTargetedMessage(result.Args![0], result.Args[1]);
                    break;

                case CommandType.Peers:
                    HandlePeers();
                    break;

                case CommandType.History:
                    _messageHistory!.ShowHistory();
                    break;

                case CommandType.Quit:
                    running = false;
                    break;

                case CommandType.Unknown:
                default:
                    _ui.DisplaySystem(result.Message ?? "Unknown command");
                    break;
            }
        }

        await ShutdownAsync();
    }

    private static void WireEvents()
    {
        _server!.OnClientConnected += endpoint =>
        {
            EndpointToUsername[endpoint] = endpoint;
            PendingIdentityEndpoints.Enqueue(endpoint);
            _ui!.DisplaySystem($"Inbound peer connected: {endpoint}");
        };

        _server.OnClientDisconnected += endpoint =>
        {
            if (EndpointToUsername.TryRemove(endpoint, out string? username))
            {
                if (!string.Equals(username, endpoint, StringComparison.OrdinalIgnoreCase))
                {
                    UsernameToEndpoint.TryRemove(username, out _);
                    _heartbeatMonitor!.StopMonitoring(username);
                }
            }

            _ui!.DisplaySystem($"Peer disconnected: {endpoint}");
        };

        _server.OnMessageReceived += HandleIncomingMessage;
        _server.OnClientJoinedRoom += (endpoint, room) =>
        {
            KnownRooms[room] = 1;
            _ui!.DisplayRoomEvent(room, $"{DisplayNameForEndpoint(endpoint)} joined");
        };
        _server.OnClientLeftRoom += (endpoint, room) =>
        {
            _ui!.DisplayRoomEvent(room, $"{DisplayNameForEndpoint(endpoint)} left");
        };

        _client!.OnConnected += endpoint =>
        {
            _ui!.DisplaySystem($"Connected to peer {endpoint}");
            if (_lastOutboundPeer is not null)
            {
                _lastOutboundPeer.IsConnected = true;
            }

            SendSigningPublicKey();
        };

        _client.OnDisconnected += endpoint =>
        {
            _ui!.DisplaySystem($"Disconnected from peer {endpoint}");
            if (_lastOutboundPeer is not null)
            {
                _lastOutboundPeer.IsConnected = false;
            }

            if (!_isShuttingDown && _lastOutboundPeer is not null)
            {
                _ = AttemptReconnectAsync(_lastOutboundPeer);
            }
        };

        _client.OnMessageReceived += HandleIncomingMessage;

        _peerDiscovery!.OnPeerDiscovered += peer =>
        {
            KnownPeers[peer.Id] = peer;
            _ui!.DisplaySystem($"Discovered peer {peer.Id} at {peer.Address}:{peer.Port}");
        };

        _peerDiscovery.OnPeerLost += peer =>
        {
            if (KnownPeers.TryGetValue(peer.Id, out Peer? existing))
            {
                existing.IsConnected = false;
            }

            _ui!.DisplaySystem($"Peer {peer.Id} stopped broadcasting discovery packets");
        };

        _heartbeatMonitor!.OnConnectionFailed += peerId =>
        {
            _ui!.DisplaySystem($"Heartbeat timeout detected for {peerId}");
            if (!_isShuttingDown && _lastOutboundPeer is not null)
            {
                _ = AttemptReconnectAsync(_lastOutboundPeer);
            }
        };

        _reconnectionPolicy!.OnReconnectAttempt += (peerId, attempt) =>
        {
            _ui!.DisplaySystem($"Reconnect attempt {attempt} to {peerId}");
        };

        _reconnectionPolicy.OnReconnectSuccess += peerId =>
        {
            _ui!.DisplaySystem($"Reconnected to {peerId}");
        };

        _reconnectionPolicy.OnReconnectFailed += peerId =>
        {
            _ui!.DisplaySystem($"Failed to reconnect to {peerId} after multiple attempts");
        };
    }

    private static void StartBackgroundServices()
    {
        _heartbeatMonitor!.Start();
        _backgroundCts = new CancellationTokenSource();
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_backgroundCts.Token));
    }

    private static async Task ShutdownAsync()
    {
        _isShuttingDown = true;

        try
        {
            _backgroundCts?.Cancel();
        }
        catch
        {
        }

        if (_heartbeatTask is not null)
        {
            try
            {
                await _heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _peerDiscovery?.Stop();
        _heartbeatMonitor?.Stop();
        _client?.Disconnect();
        _server?.Stop();
        Console.WriteLine("Goodbye!");
    }

    private static async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Message heartbeat = new()
            {
                Sender = _username,
                Content = "heartbeat",
                Timestamp = DateTime.Now,
                Type = MessageType.Heartbeat
            };

            if (_client?.IsConnected == true)
            {
                _client.Send(heartbeat);
            }

            if (_server?.IsListening == true && _server.ClientCount > 0)
            {
                _server.Broadcast(heartbeat);
            }

            await Task.Delay(_heartbeatMonitor!.HeartbeatInterval, cancellationToken);
        }
    }

    private static void HandleListen(string[] args)
    {
        if (!int.TryParse(args[0], out int port))
        {
            _ui!.DisplaySystem("Error: Invalid port number");
            return;
        }

        _server!.Start(port);
        _listeningPort = port;
        _peerDiscovery!.Start(port);
        _ui!.DisplaySystem($"Listening on port {port} with peer discovery enabled");
    }

    private static async Task HandleConnectAsync(string[] args)
    {
        string host = args[0];
        if (!int.TryParse(args[1], out int port))
        {
            _ui!.DisplaySystem("Error: Invalid port number");
            return;
        }

        _lastOutboundPeer = FindOrCreatePeer(host, port);
        bool connected = await _client!.ConnectAsync(host, port, _username);
        if (!connected)
        {
            _ui!.DisplaySystem($"Error: Failed to connect to {host}:{port}");
            return;
        }

        _lastOutboundPeer.IsConnected = true;
    }

    private static void HandleCreateRoom(string roomName)
    {
        KnownRooms[roomName] = 1;

        if (_server!.IsListening)
        {
            bool created = _server.CreateRoom(roomName);
            _ui!.DisplaySystem(created
                ? $"Room '{roomName}' created"
                : $"Room '{roomName}' already exists");
            return;
        }

        if (_client!.IsConnected)
        {
            _client.Send(new Message
            {
                Sender = _username,
                Content = $"{CreateRoomPrefix}{roomName}",
                Timestamp = DateTime.Now,
                Type = MessageType.Text
            });
            _ui!.DisplaySystem($"Requested creation of room '{roomName}'");
            return;
        }

        _ui!.DisplaySystem("Error: Start listening or connect to a peer first");
    }

    private static void HandleJoinRoom(string roomName)
    {
        KnownRooms[roomName] = 1;
        _currentRoom = roomName;
        _ui!.SetCurrentRoom(roomName);

        if (_server!.IsListening)
        {
            _server.CreateRoom(roomName);
            _ui.DisplaySystem($"Joined room '{roomName}'");
            return;
        }

        if (_client!.IsConnected)
        {
            _client.Send(new Message
            {
                Sender = _username,
                Content = $"{JoinRoomPrefix}{roomName}",
                Timestamp = DateTime.Now,
                Type = MessageType.Text
            });
            _ui.DisplaySystem($"Requested to join room '{roomName}'");
            return;
        }

        _ui!.DisplaySystem("Error: Start listening or connect to a peer first");
    }

    private static void HandleLeaveRoom(string roomName)
    {
        if (_currentRoom is null || !string.Equals(_currentRoom, roomName, StringComparison.OrdinalIgnoreCase))
        {
            _ui!.DisplaySystem("Error: You are not currently in that room");
            return;
        }

        if (_client!.IsConnected)
        {
            _client.Send(new Message
            {
                Sender = _username,
                Content = $"{LeaveRoomPrefix}{roomName}",
                Timestamp = DateTime.Now,
                Type = MessageType.Text
            });
        }

        _currentRoom = null;
        _ui!.SetCurrentRoom(null);
        _ui.DisplaySystem($"Left room '{roomName}'");
    }

    private static void HandleRooms()
    {
        if (_server!.IsListening)
        {
            var rooms = _server.GetRooms();
            if (rooms.Count == 0)
            {
                _ui!.DisplaySystem("No rooms available");
                return;
            }

            foreach (var room in rooms.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                _ui!.DisplaySystem($"{room.Name} ({room.MemberCount} members)");
            }

            return;
        }

        if (KnownRooms.IsEmpty)
        {
            _ui!.DisplaySystem("No rooms known locally");
            return;
        }

        foreach (string room in KnownRooms.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _ui!.DisplaySystem(room);
        }
    }

    private static void HandleTargetedMessage(string target, string content)
    {
        if (target.StartsWith("#", StringComparison.Ordinal))
        {
            SendRoomMessage(target, content);
            return;
        }

        if (target.StartsWith("@", StringComparison.Ordinal))
        {
            SendDirectMessage(target, content);
            return;
        }

        _ui!.DisplaySystem("Error: Unknown message target");
    }

    private static void SendPlainMessage(string content)
    {
        Message message = CreateSignedTextMessage(content);

        if (_client!.IsConnected)
        {
            _client.Send(message);
            PersistLocalMessage(message);
            return;
        }

        if (_server!.IsListening)
        {
            _server.Broadcast(message);
            PersistLocalMessage(message);
            return;
        }

        _ui!.DisplaySystem("Error: Not connected to any peer");
    }

    private static void SendRoomMessage(string roomName, string content)
    {
        Message message = CreateSignedTextMessage(content, roomName);
        KnownRooms[roomName] = 1;

        if (_server!.IsListening)
        {
            _server.CreateRoom(roomName);
            _server.BroadcastToRoom(message, roomName);
            PersistLocalMessage(message);
            return;
        }

        if (_client!.IsConnected)
        {
            _client.Send(message);
            PersistLocalMessage(message);
            return;
        }

        _ui!.DisplaySystem("Error: Not connected to any peer");
    }

    private static void SendDirectMessage(string peerToken, string content)
    {
        if (string.Equals(peerToken, $"@{_username}", StringComparison.OrdinalIgnoreCase))
        {
            Message loopback = CreateSignedTextMessage(content, peerToken);
            PersistLocalMessage(loopback);
            return;
        }

        Message message = CreateSignedTextMessage(content, peerToken);

        if (_server!.IsListening && TrySendDirectToConnectedPeer(message, peerToken))
        {
            PersistLocalMessage(message);
            return;
        }

        if (_client!.IsConnected)
        {
            _client.Send(message);
            PersistLocalMessage(message);
            return;
        }

        _ui!.DisplaySystem($"Error: Peer {peerToken} is not connected");
    }

    private static void HandlePeers()
    {
        if (KnownPeers.IsEmpty)
        {
            _ui!.DisplaySystem("No peers discovered via UDP broadcast");
        }
        else
        {
            foreach (Peer peer in KnownPeers.Values.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
            {
                string status = peer.IsConnected ? "connected" : "discovered";
                _ui!.DisplaySystem($"{peer.Id} @ {peer.Address}:{peer.Port} [{status}]");
            }
        }

        if (UsernameToEndpoint.IsEmpty)
        {
            _ui!.DisplaySystem("No named peer sessions active");
            return;
        }

        foreach (var entry in UsernameToEndpoint.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            _ui!.DisplaySystem($"{entry.Key} via {entry.Value}");
        }
    }

    private static async Task AttemptReconnectAsync(Peer peer)
    {
        if (_reconnectionPolicy is null || _isShuttingDown)
        {
            return;
        }

        try
        {
            await _reconnectionPolicy.TryReconnect(peer);
        }
        catch (Exception ex)
        {
            _ui!.DisplaySystem($"Reconnect error: {ex.Message}");
        }
    }

    private static void HandleIncomingMessage(Message message)
    {
        if (message.Type == MessageType.Heartbeat)
        {
            if (!string.IsNullOrWhiteSpace(message.Sender))
            {
                _heartbeatMonitor!.RecordHeartbeat(message.Sender);
            }
            return;
        }

        if (message.Type == MessageType.KeyExchange)
        {
            if (message.PublicKey is { Length: > 0 } && !string.IsNullOrWhiteSpace(message.Sender))
            {
                PeerPublicKeys[message.Sender] = message.PublicKey;
                _ui!.DisplaySystem($"Stored signing key for {message.Sender}");
            }
            return;
        }

        if (!VerifySignatureIfPresent(message))
        {
            _ui!.DisplaySystem($"Rejected message from {message.Sender} due to invalid signature");
            return;
        }

        if (message.Content.StartsWith(CreateRoomPrefix, StringComparison.Ordinal))
        {
            string roomName = message.Content[CreateRoomPrefix.Length..];
            KnownRooms[roomName] = 1;
            if (_server!.IsListening)
            {
                _server.CreateRoom(roomName);
            }
            return;
        }

        if (message.Content.StartsWith(JoinRoomPrefix, StringComparison.Ordinal))
        {
            string roomName = message.Content[JoinRoomPrefix.Length..];
            KnownRooms[roomName] = 1;

            if (_server!.IsListening && TryGetEndpointForSender(message.Sender, out string? endpoint))
            {
                _server.JoinRoom(endpoint, roomName);
            }
            return;
        }

        if (message.Content.StartsWith(LeaveRoomPrefix, StringComparison.Ordinal))
        {
            if (_server!.IsListening && TryGetEndpointForSender(message.Sender, out string? endpoint))
            {
                _server.LeaveRoom(endpoint);
            }
            return;
        }

        if (message.Content.EndsWith("has joined the conversation", StringComparison.Ordinal))
        {
            RegisterPeerIdentity(message.Sender);
        }

        if (!string.IsNullOrWhiteSpace(message.TargetPeerId))
        {
            if (message.TargetPeerId.StartsWith("#", StringComparison.Ordinal))
            {
                KnownRooms[message.TargetPeerId] = 1;

                if (_server!.IsListening)
                {
                    _server.CreateRoom(message.TargetPeerId);
                    _server.BroadcastToRoom(message, message.TargetPeerId);
                }

                PersistRemoteMessage(message);
                return;
            }

            if (message.TargetPeerId.StartsWith("@", StringComparison.Ordinal))
            {
                if (string.Equals(message.TargetPeerId, $"@{_username}", StringComparison.OrdinalIgnoreCase))
                {
                    PersistRemoteMessage(message);
                    return;
                }

                if (_server!.IsListening && TrySendDirectToConnectedPeer(message, message.TargetPeerId))
                {
                    return;
                }

                return;
            }
        }

        PersistRemoteMessage(message);
    }

    private static void RegisterPeerIdentity(string sender)
    {
        while (PendingIdentityEndpoints.TryDequeue(out string? endpoint))
        {
            if (!EndpointToUsername.ContainsKey(endpoint))
            {
                continue;
            }

            EndpointToUsername[endpoint] = sender;
            UsernameToEndpoint[sender] = endpoint;
            _heartbeatMonitor!.StartMonitoring(sender);
            _ui!.DisplaySystem($"Peer {sender} is associated with {endpoint}");

            if (_server!.IsListening)
            {
                _server.SendToClient(new Message
                {
                    Sender = _username,
                    Type = MessageType.KeyExchange,
                    PublicKey = LocalRsa.ExportRSAPublicKey(),
                    Timestamp = DateTime.Now
                }, endpoint);
            }

            break;
        }
    }

    private static bool VerifySignatureIfPresent(Message message)
    {
        if (message.Signature is null || message.Signature.Length == 0)
        {
            return true;
        }

        if (!PeerPublicKeys.TryGetValue(message.Sender, out byte[]? publicKey))
        {
            _ui!.DisplaySystem($"No signing key available for {message.Sender}; message left unverified");
            return true;
        }

        byte[] contentBytes = Encoding.UTF8.GetBytes(message.Content);
        return Signer.VerifyData(contentBytes, message.Signature, publicKey);
    }

    private static bool TrySendDirectToConnectedPeer(Message message, string peerToken)
    {
        string peerName = peerToken.TrimStart('@');
        if (string.Equals(peerName, _username, StringComparison.OrdinalIgnoreCase))
        {
            PersistRemoteMessage(message);
            return true;
        }

        if (UsernameToEndpoint.TryGetValue(peerName, out string? endpoint))
        {
            return _server!.SendToClient(message, endpoint);
        }

        return false;
    }

    private static bool TryGetEndpointForSender(string sender, out string endpoint)
    {
        if (UsernameToEndpoint.TryGetValue(sender, out endpoint!))
        {
            return true;
        }

        endpoint = string.Empty;
        return false;
    }

    private static void PersistLocalMessage(Message message)
    {
        _ui!.DisplayMessage(message);
        _messageHistory!.SaveMessage(CloneMessage(message));
    }

    private static void PersistRemoteMessage(Message message)
    {
        _ui!.DisplayMessage(message);
        _messageHistory!.SaveMessage(CloneMessage(message));
    }

    private static void SendSigningPublicKey()
    {
        if (_client?.IsConnected != true)
        {
            return;
        }

        _client.Send(new Message
        {
            Sender = _username,
            Type = MessageType.KeyExchange,
            PublicKey = LocalRsa.ExportRSAPublicKey(),
            Timestamp = DateTime.Now
        });
    }

    private static Message CreateSignedTextMessage(string content, string? target = null)
    {
        Message message = new()
        {
            Sender = _username,
            Content = content,
            Timestamp = DateTime.Now,
            Type = MessageType.Text,
            TargetPeerId = target
        };

        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        message.Signature = Signer.SignData(contentBytes);
        return message;
    }

    private static Peer FindOrCreatePeer(string host, int port)
    {
        Peer? discovered = KnownPeers.Values.FirstOrDefault(peer =>
            string.Equals(peer.Address?.ToString(), host, StringComparison.OrdinalIgnoreCase) &&
            peer.Port == port);

        if (discovered is not null)
        {
            return discovered;
        }

        string peerId = $"{host}:{port}";
        Peer created = new()
        {
            Id = peerId,
            Name = peerId,
            Port = port,
            IsConnected = false
        };

        if (System.Net.IPAddress.TryParse(host, out System.Net.IPAddress? address))
        {
            created.Address = address;
        }

        KnownPeers[created.Id] = created;
        return created;
    }

    private static string DisplayNameForEndpoint(string endpoint)
    {
        return EndpointToUsername.TryGetValue(endpoint, out string? username)
            ? username
            : endpoint;
    }

    private static Message CloneMessage(Message message)
    {
        return new Message
        {
            Id = message.Id,
            Sender = message.Sender,
            Content = message.Content,
            Timestamp = message.Timestamp,
            Type = message.Type,
            Signature = message.Signature,
            EncryptedContent = message.EncryptedContent,
            PublicKey = message.PublicKey,
            TargetPeerId = message.TargetPeerId
        };
    }
}
