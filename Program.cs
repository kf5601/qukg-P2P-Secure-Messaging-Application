// Kai Fan
// CSCI 251 - Secure Distributed Messenger
// Group Project
//
// SPRINT 1: Threading & Basic Networking
// Due: Week 5 | Work on: Weeks 3-4
// (Continue enhancing in Sprints 2 & 3)
//

using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;

namespace SecureMessenger;

/// <summary>
/// Main entry point for the Secure Distributed Messenger.
///
/// Architecture Overview:
/// This application uses multiple threads to handle concurrent operations:
///
/// 1. Main Thread (UI Thread)
///    - Reads user input from console
///    - Parses commands using ConsoleUI
///    - Dispatches commands to appropriate handlers
///
/// 2. Accept Thread (Server)
///    - Runs Server to accept incoming connections
///    - Each accepted connection spawns a receive task
///
/// 3. Receive Task(s)
///    - One per connected client
///    - Reads messages from network
///    - Invokes OnMessageReceived event
///
/// 4. Client Receive Task
///    - Reads messages from server we connected to
///    - Invokes OnMessageReceived event
///
/// Thread Communication:
/// - Use events for connection/disconnection/message notifications
/// - Use CancellationToken for graceful shutdown
/// - (Optional) Use MessageQueue for more complex processing pipelines
///
/// Sprint Progression:
/// - Sprint 1: Basic threading and networking (connect, send, receive)
///             Uses simple Client/Server model
/// - Sprint 2: Add encryption (key exchange, AES encryption, signing)
/// - Sprint 3: Upgrade to peer-to-peer model with Peer class,
///             add peer discovery, heartbeat, and reconnection
/// </summary>
class Program
{
    // Declare your components as fields for access across methods
    // Sprint 1-2 components:
    private static Server? _server;
    private static Client? _client;
    private static ConsoleUI? _ui;
    private static string _username = "User";
    private static string? _currentRoom = null;

    private static readonly Dictionary<string, byte[]> _peerPublicKeys
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> _endpointToUsername
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _trustedPeers
        = new(StringComparer.OrdinalIgnoreCase);
    //
    // Sprint 3 additions:
    // private static PeerDiscovery? _peerDiscovery;
    // private static HeartbeatMonitor? _heartbeatMonitor;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        // Initialize components
        _server = new Server();
        _client = new Client();
        _ui = new ConsoleUI();
        // _queue = new MessageQueue(); CODE FOR SPRINT 3

        // 1. Create Server for incoming connections
        // 2. Create Client for outgoing connection
        // 3. Create ConsoleUI for user interface
        // 4. (Optional) Create MessageQueue if using producer/consumer pattern

        // TODO: Subscribe to events
        // Server events:
        
        _server.OnClientConnected += (peer) => 
        {
            // Store endpoint, username will be set when join message arrives
            _endpointToUsername[peer] = "unknown";
        };
        _server.OnClientDisconnected += (peer) =>
        {
            _ui.DisplaySystem($"Client {peer} disconnected");
            _peerPublicKeys.Remove(peer);
            _trustedPeers.Remove(peer);
            _endpointToUsername.Remove(peer);
        };

        _server.OnMessageReceived += msg => HandleIncomingMessage(msg);
        _server.OnClientJoinedRoom += (ep, room) => _ui.DisplayRoomEvent(room, $"{ep} joined the room");
        _server.OnClientLeftRoom += (ep, room) => _ui.DisplayRoomEvent(room, $"{ep} left the room");

        // Client events:
        _client.OnConnected += endPoint =>
        {
            _ui.DisplaySystem($"Connected to server {endPoint}");
            SendKeyExchange();
        };
        _client.OnDisconnected += endPoint =>
        {
            _ui.DisplaySystem($"Disconnected from server {endPoint}");
            _peerPublicKeys.Remove(endPoint);
            _trustedPeers.Remove(endPoint);
        };
        _client.OnMessageReceived += msg => HandleIncomingMessage(msg);


        Console.WriteLine("Type /help for available commands");
        Console.WriteLine();

        // Main loop - handle user input
        bool running = true;
        while (running)
        {
            // Implement the main input loop
            // 1. Read a line from the console
            // 2. Skip empty input
            // 3. Parse the input using ConsoleUI.ParseCommand()
            // 4. Handle the command based on CommandType:
            //    - Connect: Call await _client.ConnectAsync(host, port)
            //    - Listen: Call _server.Start(port)
            //    - ListPeers: Display connection status
            //    - History: Show message history (Sprint 3)
            //    - Quit: Set running = false
            //    - Not a command: Send as a message

            _ui.PrintPrompt();
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // this is the result from ui.ParseCommand, which might only return regular message
            var result = _ui.ParseCommand(input);
            if (!result.IsCommand)
            {
                if (string.IsNullOrWhiteSpace(result.Message))
                {
                    _ui.DisplaySystem("Error: Cannot send empty message");
                    continue;
                }

                SendMessage(result.Message);
                continue;
            }

            // processing command and actually executing it
            switch (result.CommandType)
            {
                case CommandType.Help:
                    _ui.ShowHelp();
                    break;

                case CommandType.Quit:
                    running = false;
                    break;

                case CommandType.Listen:
                    if (result.Args is null)
                    {
                        _ui!.DisplaySystem("Error: Missing args."); // ! is for null forgiveness, no squiggly yellow line
                        break;
                    }

                    HandleListen(result.Args);
                    break;

                case CommandType.Connect:
                    if (result.Args is null)
                    {
                        _ui!.DisplaySystem("Error: Missing args."); // ! is for null forgiveness, no squiggly yellow line
                        break;
                    }

                    await HandleConnectAsync(result.Args);
                    break;

                case CommandType.Peers:
                    HandlePeers();
                    break;

                //this case is for sprint 3
                //case CommandType.History:
                //    HandleHistory();
                case CommandType.Unknown:
                    _ui.DisplaySystem(result.Message ?? "Unknown command");
                    break;

                case CommandType.RoomCreate:
                    if (result.Args is not null) HandleRoomCreate(result.Args);
                    break;

                case CommandType.RoomJoin:
                    if (result.Args is not null) await HandleRoomJoinAsync(result.Args);
                    break;

                case CommandType.RoomLeave:
                    await HandleRoomLeaveAsync();
                    break;

                case CommandType.RoomList:
                    HandleRoomList();
                    break;

                case CommandType.Trust:
                    if (result.Args is not null) HandleTrust(result.Args);
                    break;

                case CommandType.KeyInfo:
                    HandleKeyInfo();
                    break;
            }
        }

        // Implement graceful shutdown
        // 1. Stop the server
        // 2. Disconnect the client
        // 3. (Sprint 3) Stop peer discovery and heartbeat monitor
        _server?.Stop(); // ? is for null conditional operator, only call Stop if _server is not null
        if (_client is not null) // check if client is connected before trying to disconnect
        {
            _client.Disconnect();
        }

        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Display help information.
    /// Replace this with ConsoleUI.ShowHelp() once implemented.
    /// </summary>
    // private static void ShowHelp()
    // {
    //     Console.WriteLine("\nAvailable Commands:");
    //     Console.WriteLine("  /connect <ip> <port> <name>  - Connect to another messenger");
    //     Console.WriteLine("  /listen <port>        - Start listening for connections");
    //     Console.WriteLine("  /peers                - Show connection status");
    //     Console.WriteLine("  /history              - View message history (Sprint 3)");
    //     Console.WriteLine("  /quit                 - Exit the application");
    //     Console.WriteLine();
    //     Console.WriteLine("Sprint Progression:");
    //     Console.WriteLine("  Sprint 1: Basic /connect and /listen with message sending");
    //     Console.WriteLine("  Sprint 2: Messages are encrypted end-to-end");
    //     Console.WriteLine("  Sprint 3: Automatic peer discovery and reconnection");
    //     Console.WriteLine();
    // }

    // TODO: Add helper methods as needed
    private static void HandleListen(string[] args)
    {
        if (args.Length != 1)
        {
            _ui!.DisplaySystem("Error: /listen requires 1 argument (port)"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }

        // validate port number
        if (!int.TryParse(args[0], out int port))
        {
            _ui!.DisplaySystem("Error: Invalid port number, it must be a number!"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }

        _server!.Start(port); // ! is for null forgiveness, no squiggly yellow line
        _ui!.DisplaySystem($"Started listening on port {port}"); // ! is for null forgiveness, no squiggly yellow line
    }

    private static async Task HandleConnectAsync(string[] args)
    {
        if (args.Length != 3)
        {
            _ui!.DisplaySystem("Error: /connect requires 3 arguments (host, port, and name)");
            return;
        }

        string host = args[0];
        if (!int.TryParse(args[1], out int port))
        {
            _ui!.DisplaySystem("Error: Invalid port number!");
            return;
        }

        string myName = args[2];
        _username = myName;

        bool status = await _client!.ConnectAsync(host, port, myName);
        if (!status)
        {
            _ui!.DisplaySystem($"Error: Failed to connect to {host}:{port}");
        }
    }

    private static void HandlePeers()
    {
        _ui!.DisplaySystem($"Server listening = {_server!.IsListening}, port = {_server.Port}, clients = {_server.ClientCount}"); // ! is for null forgiveness, no squiggly yellow line
        _ui.DisplaySystem($"Client connected = {_client!.IsConnected}"); // ! is for null forgiveness, no squiggly yellow line
        _ui.DisplaySystem(_currentRoom is not null ? $"Current room: {_currentRoom}" : "Not in any room.");
        _ui.DisplaySystem(_trustedPeers.Count > 0
            ? $"Trusted peers: {string.Join(", ", _trustedPeers)}"
            : "No trusted peers yet, use /trust");
    }

    private static void SendMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _ui!.DisplaySystem("Error: Cannot send empty message"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }
        Message msg = new Message
        {
            Sender = _username,
            Content = content,
            Timestamp = DateTime.Now
        };

        if (_client!.IsConnected) // ! is for null forgiveness, no squiggly yellow line
        {
            _client.Send(msg);
        }
        else if (_server!.IsListening)
        {
            if (_currentRoom is not null)
                _server.BroadcastToRoom(msg, _currentRoom);
            else
                _server.Broadcast(msg);

            _ui!.DisplayMessage(msg);
        }
        else
        {
            _ui!.DisplaySystem("Error: Not connected to any peer. Use /connect or /listen first."); // ! is for null forgiveness, no squiggly yellow line
        }
    }
    // central handler
    private static void HandleIncomingMessage(Message message)
    {
        switch (message.Type)
        {
            // store peer public key, prompts user
            case MessageType.KeyExchange:
                if (message.PublicKey is { Length: > 0 })
                {
                    _peerPublicKeys[message.Sender] = message.PublicKey;
                    _ui!.DisplaySystem(
                        $"Received public key from '{message.Sender}'. " +
                        $"Run /trust {message.Sender} to enable encryption with them");
                }
                break;

            case MessageType.SessionKey:
                _ui!.DisplaySystem($"[Encryption] Session key received from {message.Sender}");
                break;

            case MessageType.Text:
            default:
                // If this is a join message, update endpoint-to-username mapping and display with both
                if (message.Content.EndsWith("has joined the conversation"))
                {
                    // Find which endpoint this username belongs to (first unresolved one)
                    var unresolvedEndpoint = _endpointToUsername.FirstOrDefault(kvp => kvp.Value == "unknown").Key;
                    if (unresolvedEndpoint != null)
                    {
                        _endpointToUsername[unresolvedEndpoint] = message.Sender;
                        _ui!.DisplaySystem($"Client {unresolvedEndpoint} ({message.Sender}) connected");
                    }
                }
                _ui!.DisplayMessage(message);
                break;
        }
    }

    private static void HandleRoomCreate(string[] args)
    {
        string roomName = args[0];
        if (!_server!.IsListening)
        {
            _ui!.DisplaySystem("You must be listening to create rooms");
            return;
        }
        bool created = _server.CreateRoom(roomName);
        _ui!.DisplaySystem(created
            ? $"Room '{roomName}' created, peers can now /room join {roomName}."
            : $"Room '{roomName}' already exists");
    }

    private static async Task HandleRoomJoinAsync(string[] args)
    {
        string roomName = args[0];

        if (_server!.IsListening)
        {
            _currentRoom = roomName;
            _ui!.SetCurrentRoom(roomName);
            _server.CreateRoom(roomName);
            var joinMsg = SystemMessage($"{_username} joined room '{roomName}'");
            _server.BroadcastToRoom(joinMsg, roomName);

            _ui.DisplaySystem($"Joined room '{roomName}'");
        }
        else if (_client!.IsConnected)
        {
            _currentRoom = roomName;
            _ui!.SetCurrentRoom(roomName);

            var joinMsg = new Message
            {
                Sender = _username,
                Content = $"__JOIN_ROOM__{roomName}",
                Type = MessageType.Text
            };
            _client.Send(joinMsg);
            _ui.DisplaySystem($"Requested to join room '{roomName}'");
        }
        else
        {
            _ui!.DisplaySystem("Not connected, use /connect or /listen first");
        }

        await Task.CompletedTask; // keeps signature async for future awaits
    }

    // /room leave 
    private static async Task HandleRoomLeaveAsync()
    {
        if (_currentRoom is null)
        {
            _ui!.DisplaySystem("You are not in any room");
            return;
        }
        string left = _currentRoom;
        if (_server!.IsListening)
        {
            var leaveMsg = SystemMessage($"{_username} left room '{left}'");
            _server.BroadcastToRoom(leaveMsg, left);
        }
        else if (_client!.IsConnected)
        {
            var leaveMsg = new Message
            {
                Sender = _username,
                Content = $"__LEAVE_ROOM__{left}",
                Type = MessageType.Text
            };
            _client.Send(leaveMsg);
        }
        _currentRoom = null;
        _ui!.SetCurrentRoom(null);
        _ui.DisplaySystem($"Left room '{left}'");
        await Task.CompletedTask;
    }

    //room list 
    private static void HandleRoomList()
    {
        if (!_server!.IsListening)
        {
            _ui!.DisplaySystem("Room list is only available on the listening peer");
            return;
        }
        var rooms = _server.GetRooms();
        if (rooms.Count == 0)
        {
            _ui!.DisplaySystem("No rooms exist yet, create one with /room create");
            return;
        }
        _ui!.DisplaySystem($"{"Room",-20} {"Members",8}");
        _ui.DisplaySystem(new string('─', 30));
        foreach (var (name, count, members) in rooms)
        {
            _ui.DisplaySystem($"{name,-20} {count,8}");
            foreach (var m in members)
                _ui.DisplaySystem($"  └─ {m}");
        }
    }

    private static void HandleTrust(string[] args)
    {
        string peer = args[0];
        _trustedPeers.Add(peer);
        _ui!.DisplaySystem($"Peer '{peer}' is now trusted, messages to and from them will be encrypted");
    }

    private static void HandleKeyInfo()
    {
        _ui!.DisplaySystem("Key exchange is handled automatically on connect");
        _ui.DisplaySystem($"Trusted peers: {((_trustedPeers.Count > 0) ? string.Join(", ", _trustedPeers) : "none")}");
    }

    private static void SendKeyExchange()
    {
        if (!_client!.IsConnected) return; // already handled in Client.ConnectAsync
    }

    private static Message SystemMessage(string text) =>
        new Message
        {
            Sender = "[System]",
            Content = text,
            Timestamp = DateTime.Now,
            Type = MessageType.Text
        };
}
