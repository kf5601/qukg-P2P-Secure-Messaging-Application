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
    // TODO: Declare your components as fields for access across methods
    //Sprint 1-2 components:
    private static Server? _server;
    private static Client? _client;
    private static ConsoleUI? _ui;
    private static string _username = "User";
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
        _server.OnClientConnected += peer => _ui.DisplaySystem($"Client {peer} connected");
        _server.OnClientDisconnected += peer => _ui.DisplaySystem($"Client {peer} disconnected");
        _server.OnMessageReceived += msg => _ui.DisplayMessage(msg);

        // Client events:
        _client.OnConnected += EndPoint => _ui.DisplaySystem($"Connected to server {EndPoint}");
        _client.OnDisconnected += EndPoint => _ui.DisplaySystem($"Disconnected from server {EndPoint}");
        _client.OnMessageReceived += msg => _ui.DisplayMessage(msg);

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

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            // this is the resuult from ui.ParseCommand, which might only return regular message
            var result = _ui.ParseCommand(input);
            if (!result.IsCommand)
            {
                if(string.IsNullOrWhiteSpace(result.Message))
                {
                    _ui.DisplaySystem("Error: Cannot send empty message");
                    continue;
                }
                await SendMessageAsync(result.Message);
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
                    if(result.Args is null)
                    {
                        _ui!.DisplaySystem("ERROR: Missing Args."); // ! is for null forgiveness, no squiggly yellow line
                        break;
                    }
                    HandleListen(result.Args);
                    break;
                case CommandType.Connect:
                    if(result.Args is null)
                    {
                        _ui!.DisplaySystem("ERROR: Missing Args."); // ! is for null forgiveness, no squiggly yellow line
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
            }
        }

        // Implement graceful shutdown
        // 1. Stop the server
        // 2. Disconnect the client
        // 3. (Sprint 3) Stop peer discovery and heartbeat monitor
        _server?.Stop(); // ? is for null conditional operator, only call Stop if _server is not null
        if(_client is not null) // check if client is connected before trying to disconnect
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
    //     Console.WriteLine("  /connect <ip> <port>  - Connect to another messenger");
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
    // Examples:
    private static void HandleListen(string[] args)
    {
        if(args.Length != 1)
        {
            _ui!.DisplaySystem("Error: /listen requires 1 argument (port)"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }
        // validate port number
        if(!int.TryParse(args[0], out int p))
        {
            _ui!.DisplaySystem("Error: Invalid port number, it must be a number!"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }
        
        int port = int.Parse(args[0]);

        _server!.Start(port); // ! is for null forgiveness, no squiggly yellow line
        _ui!.DisplaySystem($"Started listening on port {port}"); // ! is for null forgiveness, no squiggly yellow line  
    }

    private static async Task HandleConnectAsync(string[] args)
    {
        if(args.Length != 2)
        {
            _ui!.DisplaySystem("Error: /connect requires 2 arguments (host and port)"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }
        string host = args[0]; // ip address or hostname
        // validate port number
        if(!int.TryParse(args[1], out int p))
        {
            _ui!.DisplaySystem("Error: Invalid port number, it must be a number!"); // ! is for null forgiveness, no squiggly yellow line
            return;
        }

        int port = int.Parse(args[1]); // port number

        bool status = await _client!.ConnectAsync(host, port); // ! is for null forgiveness, no squiggly yellow line
        if(!status)
        {
            _ui!.DisplaySystem($"Error: Failure to connect to {host}:{port}"); // ! is for null forgiveness, no squiggly yellow line
        }
        return;
    }
    private static void HandlePeers()
    {
        _ui!.DisplaySystem($"Server listenting = {_server!.IsListening}, port = {_server!.Port}, client = {_server!.ClientCount}"); // ! is for null forgiveness, no squiggly yellow line
        _ui.DisplaySystem($"Client connected = {_client!.IsConnected}"); // ! is for null forgiveness, no squiggly yellow line
    }
    private static async Task SendMessageAsync(string content)
    {
        if (content.Length == 0)
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
        if(_client!.IsConnected) // ! is for null forgiveness, no squiggly yellow line
        {
            _client.Send(msg);
        }
        else if(_server!.IsListening) // ! is for null forgiveness, no squiggly yellow line
        {
            _server.Broadcast(msg);
        }
        else
        {
            _ui!.DisplaySystem("Error: Not connected to any peer. Use /connect or /listen first."); // ! is for null forgiveness, no squiggly yellow line
        }
        return;
    }
}
