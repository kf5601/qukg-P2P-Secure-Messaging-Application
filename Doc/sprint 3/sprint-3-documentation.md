# Sprint 3 Documentation (Final)
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)

**Team Members:**
- ### Uday Bista | Security, Parallel Processing & Message Pipeline
    - Maintains Sprint 2 security features as the application moves into the P2P model
    - Ensures AES encryption, RSA key exchange, and message signing continue to work across peer connections
    - Supports parallel message processing through the thread-safe message queue
    - Helps validate encrypted direct messages and secure message fan-out behavior
    - Assists with demo testing for secure P2P communication
    - **Primary Files:**
        - `Security/AesEncryption.cs`
        - `Security/RsaEncryption.cs`
        - `Security/KeyExchange.cs`
        - `Security/MessageSigner.cs`
        - `Core/MessageQueue.cs`

- ### Kai Fan | P2P Application Flow, Message Model & UI Commands
    - Updates the main application flow for Sprint 3 commands and peer-oriented behavior
    - Maintains the shared message model used by text, key exchange, heartbeat, and peer discovery messages
    - Integrates `/peers`, `/history`, direct-message, and room commands into the console workflow
    - Keeps command parsing and user-facing console output consistent with the required command syntax
    - Assists with wiring Sprint 3 networking events into the user interface
    - **Primary Files:**
        - `Program.cs`
        - `Core/Message.cs`
        - `Core/Peer.cs`
        - `UI/ConsoleUI.cs`

- ### Quang Huynh | P2P Networking, Resilience & Documentation
    - Refactors networking behavior toward a peer-to-peer architecture where each instance can listen and connect
    - Maintains TCP connection handling, length-prefix framing, encrypted send/receive flow, and peer status reporting
    - Integrates peer discovery, heartbeat monitoring, and reconnection support into the networking layer
    - Documents Sprint 3 architecture, protocol behavior, resilience features, and user guide content
    - Verifies build stability and documents known limitations for the final submission
    - **Primary Files:**
        - `Network/Client.cs`
        - `Network/Server.cs`
        - `Network/PeerDiscovery.cs`
        - `Network/HeartbeatMonitor.cs`
        - `Network/ReconnectionPolicy.cs`
        - `Doc/sprint 3/sprint-3-documentation.md`

- ### Grant Keegan | Decentralized Rooms, Message History & Demo
    - Carries Sprint 2 chat-room behavior into the Sprint 3 P2P workflow
    - Maintains room creation, join, leave, room listing, and room-scoped message routing
    - Implements and tests local file-based message history for `/history`
    - Helps validate direct messages, room messages, and failure-recovery scenarios from the user perspective
    - Leads final demo preparation with 3+ peers, peer discovery, room messaging, and recovery testing
    - **Primary Files:**
        - `UI/ConsoleUI.cs`
        - `UI/MessageHistory.cs`
        - `Program.cs`
        - `Network/Server.cs`

**Date:** 04/23/2026

---

## Build & Run Instructions

### Prerequisites
- .NET 9.0 SDK
- Windows, macOS, or Linux terminal capable of running `dotnet`

### Building
```bash
dotnet build SecureMessenger.csproj
```

### Running
```bash
dotnet run --project SecureMessenger.csproj
```

Optional: set a predictable local peer name for demos.

PowerShell:
```powershell
$env:SECURE_MESSENGER_NAME="Alpha"
dotnet run --project SecureMessenger.csproj
```

### Command Line Arguments
| Argument | Description | Default |
|----------|-------------|---------|
| `SECURE_MESSENGER_NAME` | Optional environment variable to override the generated local peer name | `peer-<username>-<id>` |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/listen <port>` | Start listening for peer connections and start UDP discovery | `/listen 5000` |
| `/connect <ip> <port>` | Connect to another peer | `/connect 127.0.0.1 5000` |
| `/create #room` | Create a chat room | `/create #demo` |
| `/join #room` | Join a chat room | `/join #demo` |
| `/leave #room` | Leave the current room | `/leave #demo` |
| `/rooms` | List rooms known locally or on the listening peer | `/rooms` |
| `/msg #room message` | Send a message to a room | `/msg #demo hello room` |
| `/msg @peer message` | Send a direct message to a named peer | `/msg @Beta hello` |
| `/peers` | Show discovered peers and active named peer sessions | `/peers` |
| `/history` | Show local saved message history | `/history` |
| `/quit` | Shut down the app | `/quit` |

---

## Architecture Diagram

```text
                   UDP Discovery (port 5001)
        +-------------------------------------------+
        | "PEER:<peerId>:<tcpPort>" every 5 seconds |
        +-------------------+-----------------------+
                            |
                            v
 +------------------+   TCP + JSON + Length Prefix   +------------------+
 |   Peer Process   |<------------------------------>|   Peer Process   |
 |                  |                                 |                  |
 | ConsoleUI        |                                 | ConsoleUI        |
 | Program.cs       |                                 | Program.cs       |
 | MessageHistory   |                                 | MessageHistory   |
 | HeartbeatMonitor |                                 | HeartbeatMonitor |
 | PeerDiscovery    |                                 | PeerDiscovery    |
 | Client           |                                 | Server/Client    |
 | Server           |                                 | Security Layer    |
 +------------------+                                 +------------------+
          |                                                      |
          +---- AES encrypted payloads + RSA setup + signatures -+
```

### Component Descriptions

| Component | Responsibility |
|-----------|----------------|
| `Program.cs` | Main application flow, command dispatch, message routing, startup/shutdown |
| `ConsoleUI.cs` | Parses required commands and formats console output |
| `Client.cs` | Maintains one outbound TCP connection, performs secure handshake, sends/receives messages |
| `Server.cs` | Accepts inbound TCP peers, tracks connected clients, routes broadcasts, room traffic, and direct sends |
| `PeerDiscovery.cs` | Broadcasts and listens for UDP presence packets on port `5001` |
| `HeartbeatMonitor.cs` | Tracks last-seen heartbeat timestamps and reports timeouts |
| `ReconnectionPolicy.cs` | Retries outbound connections with exponential backoff |
| `MessageHistory.cs` | Saves and loads local message history in JSON format |
| `Security/*` | AES transport encryption, RSA key exchange, and RSA-SHA256 message signing/verification |

---

## Protocol Specification

### Connection Establishment
TCP peers use a two-stage setup:

1. Open TCP connection.
2. Exchange RSA public keys for transport security.
3. Initiator generates AES-256 session key.
4. Initiator encrypts AES key with receiver public key and sends it.
5. Both sides switch to AES-encrypted text messages.
6. After the secure transport is ready, peers announce their application signing public key.

```text
Peer A                               Peer B
  |                                    |
  |------ TCP Connect ---------------->|
  |------ RSA Public Key ------------->|
  |<----- RSA Public Key --------------|
  |------ AES Session Key (RSA) ------>|
  |<----- Application Signing Key -----|
  |------ Signed/Encrypted Messages -->|
```

### Message Flow
1. User enters command or plain text in `ConsoleUI`.
2. `Program.cs` converts the input into a `Message`.
3. Outbound text messages are signed with the sender private RSA key.
4. `Client.cs` or `Server.cs` encrypts text content with the AES session for each recipient.
5. Messages are serialized to JSON and framed as:

```text
[4 bytes: payload length][JSON bytes]
```

6. Receiver decrypts text content, verifies signature when a sender public key is known, routes by message type, and saves user-visible messages to history.

### Peer Discovery Protocol
Discovery uses UDP port `5001`.

#### Broadcast Message Format
```text
PEER:<peerId>:<tcpPort>
```

Example:
```text
PEER:fbf128dd:5071
```

#### Discovery Process
1. A peer starts listening with `/listen <port>`.
2. `PeerDiscovery` starts sending presence packets every 5 seconds.
3. The same component listens for packets from other peers.
4. New peers are added to the local known-peer list and shown with `/peers`.
5. If no discovery packet is received for 30 seconds, the peer is marked lost.

### Heartbeat Protocol
Heartbeat packets are regular protocol messages with `MessageType.Heartbeat`.

- **Interval:** 5 seconds
- **Timeout:** 15 seconds
- **Action on timeout:** log timeout, mark peer failed, and trigger outbound reconnection attempts when applicable

---

## P2P Architecture

### Peer Management
Each instance can both:
- listen for inbound TCP peers with `Server`
- connect outward to another peer with `Client`

Known peers are tracked from two sources:
- UDP-discovered peers from `PeerDiscovery`
- active named TCP sessions learned from connection identity messages

### Connection Strategy
- `/listen <port>` starts inbound peer acceptance and UDP discovery.
- `/connect <ip> <port>` creates an outbound secure TCP connection.
- The app keeps running both sides at once, so a peer can accept inbound peers while also being connected outbound.

### Message Routing
- Plain text with no target is broadcast on the current active connection path.
- `/msg #room ...` routes to the named room.
- `/msg @peer ...` routes to the named peer if that peer is connected on the listening node, otherwise it is sent over the current outbound link.
- Room membership changes are announced with control messages and applied by connected peers.

---

## Resilience Features

### Failure Detection
The project detects failures in two ways:
- TCP disconnects and send/receive failures
- heartbeat timeouts after 15 seconds of inactivity

### Automatic Reconnection
Outbound reconnection uses `ReconnectionPolicy`.

- **Initial delay:** 1 second
- **Backoff strategy:** exponential: `1s, 2s, 4s, 8s, 16s` with a `30s` cap
- **Max attempts:** 5

### Graceful Degradation
- If a peer becomes unavailable, the app stays running.
- Known peers remain listed as discovered or disconnected.
- Direct sends to unavailable peers fail with a user-visible error instead of crashing the process.

---

## Message History

### Storage Format
History is stored as a JSON array of `Message` objects.

### File Location
- Default file: `message_history.json`
- Location: project working directory

### History Commands
- `/history` prints the most recent 50 saved messages by default.
- History is loaded automatically on startup.
- Each displayed user message is saved immediately after send or receive.

---

## User Guide

### Getting Started
1. Build the project with `dotnet build SecureMessenger.csproj`.
2. Open 2-3 terminals.
3. Optionally set `SECURE_MESSENGER_NAME` differently in each terminal.
4. Start one or more peers with `/listen <port>`.
5. Connect peers with `/connect <ip> <port>`.

### Connecting to Peers
- Same machine:
  - Terminal 1: `/listen 5000`
  - Terminal 2: `/listen 5001`
  - Terminal 2: `/connect 127.0.0.1 5000`
- Check `/peers` to see discovered peers and active named sessions.

### Sending Messages
- Global text: type plain text and press Enter.
- Room message: `/msg #demo hello room`
- Direct message: `/msg @Beta hello`

### Viewing Peer Status
- `/peers` shows:
  - UDP discovered peers with port and status
  - currently named TCP peer sessions

### Troubleshooting
| Problem | Solution |
|---------|----------|
| Cannot connect to peer | Verify the other peer already ran `/listen <port>` and check firewall rules |
| UDP discovery does not show peers immediately | Wait at least one 5-second broadcast interval and ensure UDP port `5001` is not blocked |
| Direct message says peer not connected | Use `/peers` first and send to the exact peer name shown in the active session list |
| History file not updating | Confirm the app has write permission in the working directory |

---

## Features Implemented

### Core Features
- [x] P2P architecture (no fixed central server process)
- [x] Peer discovery (UDP broadcast)
- [ ] Automatic peer connection
- [x] Heartbeat monitoring
- [x] Failure detection
- [x] Automatic reconnection
- [x] Message history (file-based)
- [x] Parallel message processing

### Security Features (from Sprint 2)
- [x] AES encryption
- [x] RSA key exchange
- [x] Message signing

### Bonus Features (if implemented)
- [ ] Message relay through intermediate peers
- [ ] Encrypted history storage
- [ ] Peer persistence (save/load known peers)

---

## Testing Performed

### P2P Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| 2 peers can connect securely | Outbound peer connects and encrypted chat works | `Beta` connected to `Alpha`; encrypted and signed messages displayed correctly in scripted local test | Pass |
| Peer discovery works on localhost | Peers appear in `/peers` after UDP announcement | `Alpha` discovered `Beta` on `127.0.0.1:5071` after discovery broadcast | Pass |
| Room commands and history work | Room is created, joined, message is sent, history is saved | `/create #demo`, `/join #demo`, `/msg #demo ...`, and `/history` all worked in scripted run | Pass |
| Direct message routing works | `/msg @peer ...` reaches only the target peer | `Alpha` successfully sent `/msg @Beta direct hello` in a 2-peer test | Pass |

### Resilience Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Kill outbound peer process | Disconnect detected | Beta detected disconnect when Alpha quit | Pass |
| Reconnection after failure | Reconnect attempts begin automatically | Beta started exponential reconnection attempts after Alpha quit | Pass |
| Peer returns after failure | Connection restored | Not re-run in this finish-up pass; verify in final demo | Pending |
| 3+ peers form mesh | All peers communicate in final demo setup | Not re-run in this sandbox session; verify in final demo | Pending |

---

## Known Issues

| Issue | Description | Severity | Workaround |
|-------|-------------|----------|------------|
| Discovery timing on same machine | A peer may not appear instantly because discovery packets are sent every 5 seconds | Low | Wait one broadcast interval, then run `/peers` again |
| UDP discovery depends on network policy | Some firewalls or lab networks may block UDP broadcast | Medium | Use manual `/connect <ip> <port>` if discovery is blocked |
| Room coordination is connection-scoped | Room state is coordinated over active peer links, but there is no full mesh state reconciliation after peers miss announcements | Medium | Reissue `/create` or `/join` after reconnecting peers |
| Reconnection currently targets the outbound link | Automatic reconnect is implemented for the tracked outbound peer, not every inbound peer | Medium | Reconnect inbound peers manually if needed |

---

## Future Improvements

- Add automatic outbound connection attempts for newly discovered peers
- Persist discovered peers across restarts
- Encrypt message history at rest
- Improve room-state synchronization across larger meshes
- Add relay support for multi-hop delivery

---

## Video Demo Checklist

Your demo video (8-10 minutes) should show:
- [ ] Starting 3+ peer instances
- [ ] Peer discovery in action
- [ ] Messages between multiple peers
- [ ] Killing a peer and showing failure detection
- [ ] Automatic reconnection when peer returns
- [ ] Message history feature
- [ ] `/peers` command showing connected peers
