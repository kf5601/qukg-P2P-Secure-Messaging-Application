// Quang Huynh (qth938)
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 3: P2P & Advanced Features
// Due: Week 14 | Work on: Weeks 11-13
//
// NOTE: This file is NOT used in Sprint 1 or Sprint 2!
//
// In Sprint 1-2, users manually connect using /connect and /listen.
// In Sprint 3, this class enables automatic peer discovery:
// - Broadcasts presence on the local network via UDP
// - Discovers other messengers automatically
// - Triggers automatic connection attempts
//

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: UDP-based peer discovery using broadcast.
/// Broadcasts presence and listens for other peers on the local network.
///
/// Discovery Protocol:
/// - Message format: "PEER:{peerId}:{tcpPort}"
/// - Example: "PEER:abc12345:5000"
/// - Broadcast every 5 seconds
/// - Peers timeout after 30 seconds of no broadcasts
/// </summary>
public class PeerDiscovery
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, Peer> _knownPeers = new();
    private readonly int _broadcastPort = 5001;
    private Thread? _listenThread;
    private Thread? _broadcastThread;

    public event Action<Peer>? OnPeerDiscovered;
    public event Action<Peer>? OnPeerLost;

    public int TcpPort { get; private set; }
    public string LocalPeerId { get; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Start broadcasting presence and listening for other peers.
    ///
    /// TODO: Implement the following:
    /// 1. Store the TCP port
    /// 2. Create a new CancellationTokenSource
    /// 3. Create a UdpClient on the broadcast port
    /// 4. Enable broadcast on the UDP client
    /// 5. Create and start a thread for ListenLoop
    /// 6. Create and start a thread for BroadcastLoop
    /// 7. Start a background task for TimeoutCheckLoop
    /// </summary>
    public void Start(int tcpPort)
    {
        Stop();

        TcpPort = tcpPort;
        _cancellationTokenSource = new CancellationTokenSource();
        _udpClient = new UdpClient(AddressFamily.InterNetwork);
        _udpClient.ExclusiveAddressUse = false;
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _broadcastPort));
        _udpClient.EnableBroadcast = true;

        _listenThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "PeerDiscoveryListen"
        };
        _broadcastThread = new Thread(BroadcastLoop)
        {
            IsBackground = true,
            Name = "PeerDiscoveryBroadcast"
        };

        _listenThread.Start();
        _broadcastThread.Start();
        _ = Task.Run(TimeoutCheckLoop);
    }

    /// <summary>
    /// Periodically broadcast our presence to the network.
    ///
    /// TODO: Implement the following:
    /// 1. Create an IPEndPoint for broadcast (IPAddress.Broadcast, _broadcastPort)
    /// 2. Loop while cancellation not requested:
    ///    a. Create discovery message: "PEER:{LocalPeerId}:{TcpPort}"
    ///    b. Convert to bytes using UTF8 encoding
    ///    c. Send via UDP client to the broadcast endpoint
    ///    d. Handle SocketException (ignore broadcast errors)
    ///    e. Sleep for 5 seconds between broadcasts
    /// </summary>
    private void BroadcastLoop()
    {
        if (_udpClient == null || _cancellationTokenSource == null)
            return;

        var endpoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);
        var token = _cancellationTokenSource.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                string message = $"PEER:{LocalPeerId}:{TcpPort}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                _udpClient.Send(data, data.Length, endpoint);
            }
            catch (SocketException)
            {
                // Broadcast may be blocked on some networks; keep discovery alive.
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                break;
        }
    }

    /// <summary>
    /// Listen for peer broadcast messages.
    ///
    /// TODO: Implement the following:
    /// 1. Create an IPEndPoint for receiving (IPAddress.Any, _broadcastPort)
    /// 2. Loop while cancellation not requested:
    ///    a. Receive data from UDP client (blocks until data available)
    ///    b. Convert bytes to string using UTF8 encoding
    ///    c. If message starts with "PEER:", call ProcessDiscoveryMessage
    ///    d. Handle SocketException (ignore receive errors)
    /// </summary>
    private void ListenLoop()
    {
        if (_udpClient == null || _cancellationTokenSource == null)
            return;

        var token = _cancellationTokenSource.Token;
        var receiveEndpoint = new IPEndPoint(IPAddress.Any, _broadcastPort);

        while (!token.IsCancellationRequested)
        {
            try
            {
                byte[] data = _udpClient.Receive(ref receiveEndpoint);
                string message = Encoding.UTF8.GetString(data);
                if (message.StartsWith("PEER:", StringComparison.OrdinalIgnoreCase))
                {
                    ProcessDiscoveryMessage(message, receiveEndpoint.Address);
                }
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested)
                    break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Parse a discovery message and add/update the peer.
    ///
    /// TODO: Implement the following:
    /// 1. Split the message by ':' - format is "PEER:peerId:port"
    /// 2. Validate we have at least 3 parts
    /// 3. Extract peerId (parts[1]) and port (parts[2])
    /// 4. If peerId equals LocalPeerId, return (don't add ourselves)
    /// 5. Create a Peer object with the extracted info and current timestamp
    /// 6. Try to add to _knownPeers:
    ///    - If new peer, invoke OnPeerDiscovered
    ///    - If existing peer, update LastSeen timestamp
    /// </summary>
    private void ProcessDiscoveryMessage(string message, IPAddress senderAddress)
    {
        string[] parts = message.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return;

        string peerId = parts[1];
        if (!int.TryParse(parts[2], out int port))
            return;

        if (peerId.Equals(LocalPeerId, StringComparison.OrdinalIgnoreCase))
            return;

        bool isNew = false;
        Peer peer = _knownPeers.AddOrUpdate(
            peerId,
            _ =>
            {
                isNew = true;
                return new Peer
                {
                    Id = peerId,
                    Name = peerId,
                    Address = senderAddress,
                    Port = port,
                    LastSeen = DateTime.Now,
                    IsConnected = false
                };
            },
            (_, existing) =>
            {
                existing.Address = senderAddress;
                existing.Port = port;
                existing.LastSeen = DateTime.Now;
                return existing;
            });

        if (isNew)
        {
            OnPeerDiscovered?.Invoke(peer);
        }
    }

    /// <summary>
    /// Periodically check for peers that have timed out (no broadcast in 30 seconds).
    ///
    /// TODO: Implement the following:
    /// 1. Loop while cancellation not requested:
    ///    a. Define timeout as 30 seconds
    ///    b. Get current time
    ///    c. Iterate through _knownPeers
    ///    d. If (now - peer.LastSeen) > timeout:
    ///       - Remove from _knownPeers
    ///       - Invoke OnPeerLost
    ///    e. Delay 5 seconds between checks
    /// </summary>
    private async Task TimeoutCheckLoop()
    {
        if (_cancellationTokenSource == null)
            return;

        var token = _cancellationTokenSource.Token;
        TimeSpan timeout = TimeSpan.FromSeconds(30);

        while (!token.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;
            foreach (var entry in _knownPeers.ToArray())
            {
                if ((now - entry.Value.LastSeen) > timeout &&
                    _knownPeers.TryRemove(entry.Key, out Peer? lostPeer))
                {
                    OnPeerLost?.Invoke(lostPeer);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Get list of known peers.
    /// </summary>
    public IEnumerable<Peer> GetKnownPeers()
    {
        return _knownPeers.Values.ToList();
    }

    /// <summary>
    /// Stop discovery.
    ///
    /// TODO: Implement the following:
    /// 1. Cancel the cancellation token
    /// 2. Close the UDP client
    /// 3. Wait for threads to finish (with timeout)
    /// </summary>
    public void Stop()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch
        {
        }

        try
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
        catch
        {
        }

        try
        {
            _listenThread?.Join(1000);
            _broadcastThread?.Join(1000);
        }
        catch
        {
        }

        _udpClient = null;
        _listenThread = null;
        _broadcastThread = null;
        _cancellationTokenSource = null;
    }
}
