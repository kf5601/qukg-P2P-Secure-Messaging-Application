// Quang Huynh (qth9368)
// Kai Fan (kf5601)
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 1: Threading & Basic Networking
// Due: Week 5 | Work on: Weeks 3-4
//
// KEY CONCEPTS USED IN THIS FILE:
//   - TcpClient: initiates outgoing connections (see HINTS.md)
//   - async/await: ConnectAsync, ReadAsync, WriteAsync
//   - NetworkStream: read/write bytes over network
//   - Length-prefix framing: 4-byte length + JSON payload
//
// CLIENT vs SERVER:
//   - Server (Server.cs) waits for others to connect TO it
//   - Client (this file) connects TO other servers
//   - Test: Terminal 1 runs /listen, Terminal 2 runs /connect
//
// SPRINT PROGRESSION:
//   - Sprint 1: Basic client for outgoing connections (this file)
//   - Sprint 2: Add encryption to message sending/receiving
//   - Sprint 3: Refactor to track connections as Peer objects,
//               integrate with PeerDiscovery for automatic connections
//

using System.Linq.Expressions;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SecureMessenger.Core;
using System.IO;
using System.Threading;

// Hook up to ../Security
using SecureMessenger.Security;

namespace SecureMessenger.Network;

/// <summary>
/// TCP client that connects to a server and handles message sending/receiving.
///
/// In Sprint 1-2, this handles a single outgoing connection.
///
/// In Sprint 3, connections are upgraded to "peers" with:
/// - Richer state tracking (see Peer.cs)
/// - Automatic reconnection on disconnect
/// - Integration with PeerDiscovery
/// </summary>
public class Client
{
    // Sprint 2: Security additions
    private KeyExchange? _keyExchange;
    private AesEncryption? _aes;
    private bool _secureReady = false;

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cancellationTokenSource;
    private string _serverEndpoint = "";
    private int _disconnectedFire = 0; // 0 = not fired, 1 = fired

    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

    public event Action<string>? OnConnected;
    public event Action<string>? OnDisconnected;
    public event Action<Message>? OnMessageReceived;

    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// Connect to a server at the specified address and port.
    ///
    /// TODO: Implement the following:
    /// 1. Create a new CancellationTokenSource
    /// 2. Create a new TcpClient
    /// 3. Connect asynchronously using await _client.ConnectAsync(host, port)
    /// 4. Get the NetworkStream from the client
    /// 5. Store the endpoint string (e.g., "192.168.1.5:5000")
    /// 6. Invoke OnConnected event
    /// 7. Start ReceiveAsync on a background Task
    /// 8. Return true on success
    /// 9. Catch exceptions, log error, and return false
    ///
    /// Sprint 3: This will be enhanced to create a Peer object and
    /// register it with the connection manager for reconnection support.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port, string myName)
    {
        try
        {
            Disconnect();

            _cancellationTokenSource = new CancellationTokenSource();
            var cancel_token = _cancellationTokenSource.Token;

            _client = new TcpClient();

            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();

            // Sprint 2: perform key exchange before normal text messaging begins
            await PerformKeyExchangeAsync(cancel_token);

            _serverEndpoint = _client.Client.RemoteEndPoint?.ToString() ?? $"{host}:{port}";
            Interlocked.Exchange(ref _disconnectedFire, 0);
            OnConnected?.Invoke(_serverEndpoint);

            _ = Task.Run(ReceiveAsync, cancel_token);

            var identity = new Message
            {
                Sender = myName,
                Content = "has joined the conversation",
                Type = MessageType.Text
            };
            Send(identity);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Client] Error connecting to {host}:{port} - {ex.Message}");
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Receive loop - runs on background thread.
    /// Uses length-prefix framing: 4 bytes for length, then JSON payload.
    ///
    /// TODO: Implement the following:
    /// 1. Create a 4-byte buffer for reading message length
    /// 2. Loop while not cancelled and client is connected:
    ///    a. Read 4 bytes for the message length
    ///    b. If bytesRead == 0, server disconnected - break
    ///    c. Convert bytes to int using BitConverter.ToInt32
    ///    d. Validate length (> 0 and < 1,000,000)
    ///    e. Create a buffer for the message payload
    ///    f. Read the full payload (may require multiple reads)
    ///    g. Convert to string using Encoding.UTF8.GetString
    ///    h. Deserialize JSON to Message using JsonSerializer.Deserialize
    ///    i. Invoke OnMessageReceived event
    /// 3. Catch OperationCanceledException (normal shutdown)
    /// 4. Catch other exceptions and log them
    /// 5. In finally block, invoke OnDisconnected event
    ///
    /// Sprint 3: Will be enhanced to update Peer.LastSeen and
    /// trigger reconnection attempts on unexpected disconnect.
    /// </summary>
    private async Task ReceiveAsync()
    {
        // snapshot references so they dont change mid-loop
        var cancel_token = _cancellationTokenSource?.Token ?? CancellationToken.None;
        var stream = _stream;
        var client = _client;
        try
        {
            if (stream == null || client == null)
                return;

            var lengthBuffer = new byte[4];
            while (!cancel_token.IsCancellationRequested && client.Connected)
            {
                bool gotLen = await ReadExactAsync(stream, lengthBuffer, 4, cancel_token);
                if (!gotLen)
                {
                    break; // disconnected
                }

                int length = BitConverter.ToInt32(lengthBuffer, 0);
                if (length <= 0 || length > 1_000_000) // validate length
                {
                    Console.WriteLine($"[Client] Invalid message length: {length}");
                    break;
                }

                var payloadBuffer = new byte[length]; // read payload
                bool gotPayload = await ReadExactAsync(stream, payloadBuffer, length, cancel_token);
                if (!gotPayload)
                {
                    break; // disconnected
                }

                string json = Encoding.UTF8.GetString(payloadBuffer);
                Message? msg = null;
                try
                {
                    msg = JsonSerializer.Deserialize<Message>(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Client] Error deserializing message: {ex.Message}");
                    continue; // skip this message
                }

                if (msg != null)
                {
                    // Sprint 2: protocol/setup messages are handled during handshake,
                    // and normal text messages are decrypted here if needed.
                    if (_secureReady && _aes != null && msg.Type == MessageType.Text && msg.EncryptedContent != null)
                    {
                        try
                        {
                            msg.Content = _aes.Decrypt(msg.EncryptedContent);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Client] Decryption failed: {ex.Message}");
                            continue;
                        }
                    }

                    OnMessageReceived?.Invoke(msg);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path
        }
        catch (ObjectDisposedException)
        {
            // Stream/client disposed during shutdown
        }
        catch (IOException)
        {
            // Network stream closed/reset
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Client] Receive loop error: {ex.Message}");
        }
        finally
        {
            // ensure resources are closed and event fires exactly once
            var endpoint = _serverEndpoint;
            DisconnectInternal();
            if (!string.IsNullOrWhiteSpace(endpoint) && Interlocked.Exchange(ref _disconnectedFire, 1) == 0)
            {
                OnDisconnected?.Invoke(endpoint);
            }
        }
    }

    /// <summary>
    /// Send a message to the server.
    ///
    /// TODO: Implement the following:
    /// 1. Check if connected - if not, log error and return
    /// 2. Serialize the message to JSON using JsonSerializer.Serialize
    /// 3. Convert to bytes using Encoding.UTF8.GetBytes
    /// 4. Create a 4-byte length prefix using BitConverter.GetBytes
    /// 5. Write the length prefix to the stream
    /// 6. Write the payload to the stream
    /// 7. Handle exceptions
    ///
    /// Sprint 2: Add encryption before serialization
    /// Sprint 3: Will send to Peer instead of raw stream
    /// </summary>
    public void Send(Message message)
    {
        _ = SendAsync(message); // fire and forget wrapper so Send stays synchronous
    }

    /// <summary>
    /// Asynchronous send implementation.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task SendAsync(Message message)
    {
        try
        {
            var stream = _stream;
            var client = _client;

            if (stream == null || client == null || !client.Connected)
            {
                Console.WriteLine("[Client] Send failed: not connected");
                return;
            }

            // Sprint 2: Add encryption before serialization
            // Only encrypt normal text messages after the secure session is established.
            Message outbound = message;
            if (_secureReady && _aes != null && message.Type == MessageType.Text && !string.IsNullOrEmpty(message.Content))
            {
                byte[] encrypted = _aes.Encrypt(message.Content);

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

            byte[] prefix = BitConverter.GetBytes(payload.Length); // length-prefix framing (4-byte little-endian int)

            await _sendLock.WaitAsync();
            try
            {
                await stream.WriteAsync(prefix, 0, prefix.Length);
                await stream.WriteAsync(payload, 0, payload.Length);
                await stream.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }
        catch (ObjectDisposedException)
        {
            // Disconnect in progress
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Client] Send IO error: {ex.Message}");
            Disconnect(); // likely dead connection
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Client] Send error: {ex.Message}");
        }
    }

    // Sprint 2: key exchange handshake.
    // This uses the existing length-prefix + JSON transport, but sends protocol
    // messages before normal encrypted text messaging begins.
    private async Task PerformKeyExchangeAsync(CancellationToken ct)
    {
        if (_stream == null)
            throw new InvalidOperationException("Network stream is not available for key exchange.");

        _keyExchange = new KeyExchange();

        // 1. Send public key
        var publicKeyMessage = new Message
        {
            Type = MessageType.KeyExchange,
            PublicKey = _keyExchange.GetPublicKey()
        };
        await SendPacketAsync(_stream, publicKeyMessage, ct);

        // 2. Receive server public key
        var serverPublicKeyMessage = await ReceivePacketAsync(_stream, ct);
        if (serverPublicKeyMessage?.Type != MessageType.KeyExchange || serverPublicKeyMessage.PublicKey == null)
            throw new InvalidOperationException("Invalid server public key response.");

        _keyExchange.ReceivePublicKey(serverPublicKeyMessage.PublicKey);

        // 3. Create and send encrypted AES session key
        var encryptedSessionKey = _keyExchange.CreateEncryptedSessionKey();
        var sessionKeyMessage = new Message
        {
            Type = MessageType.SessionKey,
            EncryptedContent = encryptedSessionKey
        };
        await SendPacketAsync(_stream, sessionKeyMessage, ct);

        _keyExchange.Complete();

        if (_keyExchange.SessionKey == null)
            throw new InvalidOperationException("Key exchange completed without a session key.");

        _aes = new AesEncryption(_keyExchange.SessionKey);
        _secureReady = true;
    }

    /// <summary>
    /// Disconnect from the server.
    ///
    /// TODO: Implement the following:
    /// 1. Cancel the cancellation token
    /// 2. Close the stream
    /// 3. Close the client
    /// </summary>
    public void Disconnect()
    {
        var endpoint = _serverEndpoint;
        DisconnectInternal();
        if (!string.IsNullOrWhiteSpace(endpoint) && Interlocked.Exchange(ref _disconnectedFire, 1) == 0)
        {
            OnDisconnected?.Invoke(endpoint);
        }
    }

    /// <summary>
    /// Internal disconnect logic.
    /// </summary>
    private void DisconnectInternal()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch { }

        try
        {
            _stream?.Close();
            _stream?.Dispose();
        }
        catch { }

        try
        {
            _client?.Close();
            _client?.Dispose();
        }
        catch { }

        _client = null;
        _stream = null;
        _cancellationTokenSource = null;
        _serverEndpoint = "";
        _keyExchange = null;
        _aes = null;
        _secureReady = false;
    }

    /// <summary>
    /// Reads exactly 'count' bytes unless the stream ends or cancellation is requested.
    /// Returns false if remote closed (ReadAsync returns 0) before full read.
    /// </summary>
    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, ct);
            if (read == 0)
            {
                return false; // remote closed
            }
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
}