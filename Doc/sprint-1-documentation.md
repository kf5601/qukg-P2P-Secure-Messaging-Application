# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)

**Team Members:**
- ### Uday Bista | Security & Encryption Engineer
    - Defines the security boundaries and assumptions for the initial networking layer
    - Designs message formats with future encryption hooks (plaintext for Sprint 1)
    - Advises on safe handling of sensitive data in memory (no hardcoded secrets, clean buffers)
    - Documents security considerations and threat model relevant to TCP communication
    - Prepares interface contracts for encryption to be integrated in later sprints
- ### Kai Fan | Team Lead / Integration & Architecture
    - Defines overall Sprint 1 architecture (UI ↔ Message Queue ↔ Networking)
    - Coordinates responsibilities across threading, networking, and UI modules
    - Oversees Program.cs main loop, lifecycle flow, and event wiring
    - Ensures modules interact only through clean interfaces and events
    - Manages GitHub workflow, reviews PRs, and ensures Sprint 1 deliverables are met
 - ### Quang Huynh | Networking & Concurrency Engineer / Documentation
    - Implements TCP server and client logic using TcpListener and TcpClient
    - Builds multi-threaded send/receive loops for active connections
    - Implements the thread-safe MessageQueue (producer/consumer pattern)
    - Handles connection lifecycle management (connect, disconnect, errors)
    - Debugs race conditions, deadlocks, and blocking issues
    - Writes Sprint 1 technical documentation (sprint-1-documentation.md)
- ### Grant Keegan | UI & Testing Engineer
    - Implements console-based UI and command parsing logic
    - Ensures UI runs independently of networking threads
    - Displays inbound/outbound messages via events
    - Tests thread safety, invalid commands, disconnect scenarios
    - Prepares Sprint 1 demo and validates end-to-end message flow

**Date:** 02/13/2026

---

## Build Instructions

### Prerequisites
- [List required software, e.g., .NET SDK version]
- [Any other dependencies]

### Building the Project
```
[Commands to build the project]
```

---

## Run Instructions

### Starting the Application
```
[Commands to run the application]
```

### Command Line Arguments (if any)
| Argument | Description | Example |
|----------|-------------|---------|
| | | |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Start listening for connections | `/listen 5000` |
| `/quit` | Exit the application | `/quit` |
| | | |

---

## Architecture Overview

### Threading Model
[Describe your threading approach - which threads exist and what each does]

- **Main Thread:** [Purpose]
- **Receive Thread:** [Purpose]
- **Send Thread:** [Purpose]
- [Additional threads...]

### Thread-Safe Message Queue
[Describe your message queue implementation and synchronization approach]

---

## Features Implemented

- [ ] Multi-threaded architecture
- [ ] Thread-safe message queue
- [ ] TCP server (listen for connections)
- [ ] TCP client (connect to peers)
- [ ] Send/receive text messages
- [ ] Graceful disconnection handling
- [ ] Console UI with commands

---

## Testing Performed

### Test Cases
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Two instances can connect | Connection established | | |
| Messages sent and received | Message appears on other instance | | |
| Disconnection handled | No crash, appropriate message | | |
| Thread safety under load | No race conditions | | |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (3-5 minutes) should show:
- [ ] Starting two instances of the application
- [ ] Connecting the instances
- [ ] Sending messages in both directions
- [ ] Disconnecting gracefully
- [ ] (Optional) Showing thread-safe behavior under load
