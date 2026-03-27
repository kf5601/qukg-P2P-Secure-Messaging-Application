// Quang Huynh (qth9368)
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 1: Threading & Basic Networking
// Due: Week 5 | Work on: Weeks 3-4
//
// KEY CONCEPTS USED IN THIS FILE:
//   - TcpListener: accepts incoming connections (see HINTS.md)
//   - Threads/Tasks: accept loop runs on background thread
//   - Events (Action<T>): notify Program.cs when things happen
//   - Locking: protect _clients list from concurrent access
//
// SPRINT PROGRESSION:
//   - Sprint 1: Basic server with client connections (this file)
//   - Sprint 2: Add encryption to message sending/receiving
//   - Sprint 3: Refactor to use Peer class for richer connection tracking,
//               add heartbeat monitoring and reconnection support
//

using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SecureMessenger.Core;
using System.IO;
using System.Threading;
// SPRINT 2: Hook up to ../Security
using SecureMessenger.Security;

namespace SecureMessenger.Network;

/// <summary>
/// TCP server that listens for incoming connections.
///
/// In Sprint 1-2, we use simple client/server terminology:
/// - Server listens for incoming connections
/// - Connected parties are tracked as "clients"
///
/// In Sprint 3, this evolves to peer-to-peer:
/// - Connections become "peers" with richer state (see Peer.cs)
/// - Add peer discovery, heartbeats, and reconnection
/// </summary>
public class Server
{
    // Sprint 2: per-client security
    private readonly Dictionary<TcpClient, KeyExchange> _keyExchanges = new();
    private readonly Dictionary<TcpClient, AesEncryption> _aesSessions = new();
    private readonly Dictionary<TcpClient, string> _clientNames = new();

    private TcpListener? _listener;
    private readonly List<TcpClient> _clients = new();
    private readonly object _clientsLock = new();
    private CancellationTokenSource? _cancellationTokenSource;

    // Events: invoke these with OnXxx?.Invoke(...) when something happens
    // Program.cs subscribes with: server.OnXxx += (args) => { ... };
    public event Action<string, string>? OnClientConnected;      // endpoint + display name
    public event Action<string, string>? OnClientDisconnected;
    public event Action<Message>? OnMessageReceived;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    /// <summary>
    /// Start listening for incoming connections on the specified port.
    ///
    /// TODO: Implement the following:
    /// 1. Store the port number in the Port property
    /// 2. Create a new CancellationTokenSource
    /// 3. Create a TcpListener on IPAddress.Any and the specified port
    /// 4. Call Start() on the listener
    /// 5. Set IsListening to true
    /// 6. Start AcceptClientsAsync on a background Task
    /// 7. Print a message indicating the server is listening
    /// </summary>
    public void Start(int port)
    {
        try
        {
            if (IsListening)  // if already listening, stop so no leak tasks/sockets
            {
                Stop();
            }
            Port = port;
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            IsListening = true;

            _ = Task.Run(AcceptClientsAsync); // start accept loop in background
            Console.WriteLine($"Server listening on port {port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting server on port {port}: {ex.Message}");
            Stop();
        }
    }

    /// <summary>
    /// Main loop that accepts incoming connections.
    ///
    /// TODO: Implement the following:
    /// 1. Loop while cancellation is not requested
    /// 2. Use await _listener.AcceptTcpClientAsync(_cancellationTokenSource.Token)
    /// 3. Get the endpoint string from client.Client.RemoteEndPoint
    /// 4. Add the client to _clients (with proper locking)
    /// 5. Invoke OnClientConnected event with the endpoint
    /// 6. Start ReceiveFromClientAsync for this client on a background Task
    /// 7. Catch OperationCanceledException (normal shutdown - just break)
    /// 8. Catch other exceptions and log them
    /// </summary>
    private async Task AcceptClientsAsync()
    {
        var cancel_token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            if (_listener == null)
            {
                return;
            }
            while (!cancel_token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException)
                {
                    break; // normal shutdown
                }
                catch (SocketException)
                {
                    break; // listener stopped / interrupted
                }

                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                lock (_clientsLock)
                {
                    _clients.Add(client);
                }
                _ = Task.Run(() => ReceiveFromClientAsync(client, endpoint), cancel_token); // start pr-client receive loop
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AcceptClientsAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Receive loop for a specific client - reads messages until disconnection.
    ///
    /// TODO: Implement the following:
    /// 1. Get the NetworkStream from the client
    /// 2. Create a 4-byte buffer for reading message length
    /// 3. Loop while not cancelled and client is connected:
    ///    a. Read 4 bytes for the message length (length-prefix framing)
    ///    b. If bytesRead == 0, client disconnected - break
    ///    c. Convert bytes to int using BitConverter.ToInt32
    ///    d. Validate length (> 0 and < 1,000,000)
    ///    e. Create a buffer for the message payload
    ///    f. Read the full payload (may require multiple reads)
    ///    g. Convert to string using Encoding.UTF8.GetString
    ///    h. Deserialize JSON to Message using JsonSerializer.Deserialize
    ///    i. Invoke OnMessageReceived event
    /// 4. Catch OperationCanceledException (normal shutdown)
    /// 5. Catch other exceptions and log them
    /// 6. In finally block, call DisconnectClient
    ///
    /// Sprint 3: This method will be enhanced to work with Peer objects
    /// instead of raw TcpClient, enabling richer connection state tracking.
    /// </summary>
    private async Task ReceiveFromClientAsync(TcpClient client, string endpoint)
    {
        var cancel_token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        try
        {
            using NetworkStream stream = client.GetStream();

            // Sprint 2: perform RSA public key exchange + AES session key setup
            // before entering the normal message receive loop.
            var keyExchange = new KeyExchange();
            _keyExchanges[client] = keyExchange;

            // 1. Receive client public key
            var clientPublicKeyMessage = await ReceivePacketAsync(stream, cancel_token);
            if (clientPublicKeyMessage?.Type != MessageType.KeyExchange || clientPublicKeyMessage.PublicKey == null)
                throw new InvalidOperationException($"Invalid client public key message from {endpoint}");

            keyExchange.ReceivePublicKey(clientPublicKeyMessage.PublicKey);

            // 2. Send server public key
            var serverPublicKeyMessage = new Message
            {
                Type = MessageType.KeyExchange,
                PublicKey = keyExchange.GetPublicKey()
            };
            await SendPacketAsync(stream, serverPublicKeyMessage, cancel_token);

            // 3. Receive encrypted AES session key
            var sessionKeyMessage = await ReceivePacketAsync(stream, cancel_token);
            if (sessionKeyMessage?.Type != MessageType.SessionKey || sessionKeyMessage.EncryptedContent == null)
                throw new InvalidOperationException($"Invalid session key message from {endpoint}");

            keyExchange.ReceiveEncryptedSessionKey(sessionKeyMessage.EncryptedContent);

            if (keyExchange.SessionKey == null)
                throw new InvalidOperationException($"Session key was not established for {endpoint}");

            var aes = new AesEncryption(keyExchange.SessionKey);
            _aesSessions[client] = aes;

            byte[] lengthBuffer = new byte[4];
            while (!cancel_token.IsCancellationRequested && client.Connected)
            {
                // Read 4 bytes for message length
                bool lengthRead = await ReadExactAsync(stream, lengthBuffer, 4, cancel_token);
                if (!lengthRead)
                {
                    break; // client disconnected
                }

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength <= 0 || messageLength >= 1_000_000)
                {
                    Console.WriteLine($"Invalid message length {messageLength} from {endpoint}");
                    break;
                }

                // Read the full message payload
                byte[] payloadBuffer = new byte[messageLength];
                bool payloadRead = await ReadExactAsync(stream, payloadBuffer, messageLength, cancel_token);
                if (!payloadRead)
                {
                    break; // client disconnected
                }

                string json = Encoding.UTF8.GetString(payloadBuffer);
                Message? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<Message>(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing message from {endpoint}: {ex.Message}");
                    continue;
                }

                if (message != null)
                {
                    RegisterClientName(client, endpoint, message);

                    // Sprint 2: decrypt normal text messages after handshake is established.
                    if (message.Type == MessageType.Text && message.EncryptedContent != null)
                    {
                        var clientAes = _aesSessions[client];
                        message.Content = clientAes.Decrypt(message.EncryptedContent);
                    }

                    OnMessageReceived?.Invoke(message);
                    Broadcast(message, client); // echo to all clients except sender
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed during shutdown
        }
        catch (IOException)
        {
            // Connection reset/closed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Receive loop error from {endpoint}: {ex.Message}");
        }
        finally
        {
            DisconnectClient(client, endpoint);
        }
    }

    /// <summary>
    /// Clean up a disconnected client.
    ///
    /// TODO: Implement the following:
    /// 1. Remove the client from _clients (with proper locking)
    /// 2. Close the client connection
    /// 3. Invoke OnClientDisconnected event
    ///
    /// Sprint 3: This will be refactored to DisconnectPeer(Peer peer)
    /// to handle richer peer state and trigger reconnection attempts.
    /// </summary>
    private void DisconnectClient(TcpClient client, string endpoint)
    {
        bool removed = false;
        string clientName = endpoint;
        lock (_clientsLock)
        {
            removed = _clients.Remove(client);
            if (_clientNames.TryGetValue(client, out var storedName) && !string.IsNullOrWhiteSpace(storedName))
            {
                clientName = storedName;
            }
            _clientNames.Remove(client);
        }

        _aesSessions.Remove(client);
        _keyExchanges.Remove(client);

        try
        {
            client.Close();
            client.Dispose();
        }
        catch { }

        if (removed)
        {
            OnClientDisconnected?.Invoke(endpoint, clientName);
        }
    }

    /// <summary>
    /// Send a message to all connected clients (broadcast).
    ///
    /// TODO: Implement the following:
    /// 1. Serialize the message to JSON using JsonSerializer.Serialize
    /// 2. Convert to bytes using Encoding.UTF8.GetBytes
    /// 3. Create a 4-byte length prefix using BitConverter.GetBytes
    /// 4. Get a copy of _clients (with proper locking)
    /// 5. For each connected client:
    ///    a. Get the NetworkStream
    ///    b. Write the length prefix (4 bytes)
    ///    c. Write the payload
    /// 6. Handle exceptions for individual clients (don't stop broadcast)
    /// </summary>
    public void Broadcast(Message message, TcpClient? sender = null)
    {
        List<TcpClient> clientsCopy;
        lock (_clientsLock)
        {
            clientsCopy = _clients.ToList();
        }

        foreach (TcpClient client in clientsCopy)
        {
            if (client == sender) continue;

            try
            {
                NetworkStream stream = client.GetStream();
                Message outbound = message;

                // Sprint 2: encrypt separately for each connected client using that
                // client's established AES session key.
                if (message.Type == MessageType.Text && _aesSessions.TryGetValue(client, out var aes))
                {
                    byte[] encrypted = aes.Encrypt(message.Content);

                    outbound = new Message
                    {
                        Id = message.Id,
                        Sender = message.Sender,
                        Content = "",
                        EncryptedContent = encrypted,
                        Timestamp = message.Timestamp,
                        Type = message.Type,
                        Signature = message.Signature,
                        PublicKey = message.PublicKey,
                        TargetPeerId = message.TargetPeerId
                    };
                }

                string json = JsonSerializer.Serialize(outbound);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

                stream.Write(lengthPrefix, 0, 4);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting to client: {ex.Message}");
                var badEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                DisconnectClient(client, badEndpoint);
            }
        }
    }

    /// <summary>
    /// Stop the server and close all connections.
    ///
    /// TODO: Implement the following:
    /// 1. Cancel the cancellation token
    /// 2. Stop the listener
    /// 3. Set IsListening to false
    /// 4. Close all clients (with proper locking)
    /// 5. Clear the _clients list
    /// </summary>
    public void Stop()
    {
        try { _cancellationTokenSource?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }

        IsListening = false;

        List<TcpClient> snapshot;
        lock (_clientsLock)
        {
            snapshot = _clients.ToList();
            _clients.Clear();
            _clientNames.Clear();
        }

        foreach (var client in snapshot)
        {
            var ep = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            try { client.Close(); client.Dispose(); } catch { }
        }

        _aesSessions.Clear();
        _keyExchanges.Clear();

        _listener = null;
        _cancellationTokenSource = null;
        Port = 0;
        Console.WriteLine("Server stopped");
    }

    /// <summary>
    /// Get the count of currently connected clients.
    /// </summary>
    public int ClientCount
    {
        get
        {
            lock (_clientsLock)
            {
                return _clients.Count;
            }
        }
    }

    /// <summary>
    /// Reads exactly 'count' bytes unless the stream ends or cancellation is requested
    /// Returns false if remote closed (ReadAsync returns 0) before full read
    /// </summary>
    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, ct);
            if (read == 0)
                return false;

            offset += read;
        }
        return true;
    }

    // Sprint 2: helper used only for protocol packets during the handshake.
    private static async Task SendPacketAsync(NetworkStream stream, Message message, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        byte[] prefix = BitConverter.GetBytes(payload.Length);

        await stream.WriteAsync(prefix, 0, prefix.Length, ct);
        await stream.WriteAsync(payload, 0, payload.Length, ct);
        await stream.FlushAsync(ct);
    }

    // Sprint 2: helper used only for protocol packets during the handshake.
    private static async Task<Message?> ReceivePacketAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] lengthBuffer = new byte[4];
        bool gotLen = await ReadExactAsync(stream, lengthBuffer, 4, ct);
        if (!gotLen)
            return null;

        int length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length <= 0 || length > 1000000)
            throw new InvalidOperationException($"Invalid packet length during handshake: {length}");

        byte[] payloadBuffer = new byte[length];
        bool gotPayload = await ReadExactAsync(stream, payloadBuffer, length, ct);
        if (!gotPayload)
            return null;

        string json = Encoding.UTF8.GetString(payloadBuffer);
        return JsonSerializer.Deserialize<Message>(json);
    }

    private void RegisterClientName(TcpClient client, string endpoint, Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Sender))
        {
            return;
        }

        bool shouldFireConnected = false;
        lock (_clientsLock)
        {
            if (!_clientNames.ContainsKey(client))
            {
                _clientNames[client] = message.Sender;
                shouldFireConnected = true;
            }
        }

        if (shouldFireConnected)
        {
            OnClientConnected?.Invoke(endpoint, message.Sender);
        }
    }
}
