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
    - Carries Sprint 2 chat-room behavior into the Sprint 3 P2P workflow (also fix up mistakes in previous implementation of chatroom)
    - Maintains room creation, join, leave, room listing, and room-scoped message routing
    - Implements and tests local file-based message history for `/history`
    - Helps validate direct messages, room messages, and failure-recovery scenarios from the user perspective
    - Leads final demo preparation with 3+ peers, peer discovery, room messaging, and recovery testing
    - **Primary Files:**
        - `UI/ConsoleUI.cs`
        - `UI/MessageHistory.cs`
        - `Program.cs`
        - `Network/Server.cs`

**Date:** 04/24/2026

---

## Build & Run Instructions

### Prerequisites
- [List all required software]

### Building
```
[Commands to build]
```

### Running
```
[Commands to run]
```

### Command Line Arguments
| Argument | Description | Default |
|----------|-------------|---------|
| | | |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Start listening | `/listen 5000` |
| `/peers` | List known peers | `/peers` |
| `/history` | View message history | `/history` |
| `/quit` | Exit application | `/quit` |
| | | |

---

## Architecture Diagram

```
[Insert ASCII diagram of your system architecture]
[Show major components and how they interact]

+------------------+     +------------------+
|                  |     |                  |
|                  |<--->|                  |
|                  |     |                  |
+------------------+     +------------------+
```

### Component Descriptions

| Component | Responsibility |
|-----------|----------------|
| | |
| | |
| | |

---

## Protocol Specification

### Connection Establishment
[Describe the full connection handshake]

```
Peer A                          Peer B
  |                                |
  |-------- [Step 1] ------------->|
  |<------- [Step 2] --------------|
  |-------- [Step 3] ------------->|
  |                                |
```

### Message Flow
[Describe how messages flow through the system]

### Peer Discovery Protocol
[Describe UDP broadcast format and discovery process]

#### Broadcast Message Format
```
[Format of discovery broadcast]
```

#### Discovery Process
1. [Step 1]
2. [Step 2]
3. ...

### Heartbeat Protocol
[Describe heartbeat mechanism]

- **Interval:** [e.g., 5 seconds]
- **Timeout:** [e.g., 15 seconds]
- **Action on timeout:** [e.g., mark as disconnected, attempt reconnect]

---

## P2P Architecture

### Peer Management
[Describe how peers are tracked and managed]

### Connection Strategy
[Describe how connections are established and maintained]

### Message Routing
[Describe how messages are routed between peers]

---

## Resilience Features

### Failure Detection
[Describe how connection failures are detected]

### Automatic Reconnection
[Describe your reconnection strategy]

- **Initial delay:** [e.g., 1 second]
- **Backoff strategy:** [e.g., exponential, max 30 seconds]
- **Max attempts:** [e.g., 5]

### Graceful Degradation
[Describe how the system behaves when peers are unavailable]

---

## Message History

### Storage Format
[Describe how messages are stored locally]

### File Location
[Where is history stored?]

### History Commands
[How users interact with history]

---

## User Guide

### Getting Started
1. [Step 1: Start the application]
2. [Step 2: ...]
3. ...

### Connecting to Peers
[Instructions for connecting]

### Sending Messages
[Instructions for messaging]

### Viewing Peer Status
[Instructions for checking peer status]

### Troubleshooting
| Problem | Solution |
|---------|----------|
| Cannot connect to peer | [Check firewall, verify IP/port] |
| Messages not sending | [Check connection status] |
| | |

---

## Features Implemented

### Core Features
- [ ] P2P architecture (no central server)
- [ ] Peer discovery (UDP broadcast)
- [ ] Automatic peer connection
- [ ] Heartbeat monitoring
- [ ] Failure detection
- [ ] Automatic reconnection
- [ ] Message history (file-based)
- [ ] Parallel message processing

### Security Features (from Sprint 2)
- [ ] AES encryption
- [ ] RSA key exchange
- [ ] Message signing

### Bonus Features (if implemented)
- [ ] Message relay through intermediate peers
- [ ] Encrypted history storage
- [ ] Peer persistence (save/load known peers)

---

## Testing Performed

### P2P Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| 3+ peers can form mesh | All peers connected | | |
| Peer discovery works | New peer found automatically | | |
| Peer leaving detected | Removed from peer list | | |
| Reconnection after failure | Connection restored | | |

### Resilience Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Kill peer process | Detected as failed | | |
| Network interruption | Reconnection attempted | | |
| Peer rejoins | Connection restored | | |

---

## Known Issues

| Issue | Description | Severity | Workaround |
|-------|-------------|----------|------------|
| | | | |

---

## Future Improvements

[What would you improve with more time?]

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
