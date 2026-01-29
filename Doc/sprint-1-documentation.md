# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)

**Team Members:**
- ### Uday Bista | Security & Encryption Engineer
    - Defines Sprint 1 security boundaries and assumptions for the TCP networking layer (plaintext in Sprint 1)
    - Designs message framing/format conventions compatible with the provided length-prefix examples in HINTS.md
    - Ensures safe handling of sensitive data practices (no hardcoded secrets, minimize logging of private content, clear buffers where appropriate)
    - Maintains a lightweight threat model focused on TCP communication risks and misuse cases
    - Prepares interface contracts and extension points for adding encryption/authentication in later sprints
- ### Kai Fan | Team Lead / Integration & Architecture
    - Owns overall Sprint 1 architecture and integration flow: Console UI ↔ event handlers ↔ Networking (Server/Client)
    - Oversees Program.cs lifecycle: startup, command routing, and wiring events (e.g., `OnClientConnected`, message-received callbacks)
    - Ensures modules communicate through clean interfaces and events (avoids tight coupling between UI and networking internals)
    - Manages Git workflow (branching strategy, PR reviews, merge discipline) and ensures Sprint 1 deliverables match rubric
 - ### Quang Huynh | Networking & Concurrency Engineer / Documentation
    - Implements core TCP networking using classes:
        - `Server` for listening and accepting connections
        - `Client` for per-connection send/receive logic
    - Implements connection lifecycle handling: `accept/connect`, `disconnect`, `exceptions`, and `cleanup`
    - Builds multi-threaded send/receive loops and ensures no blocking or deadlocks during shutdown
    - Integrates length-prefix framing and message parsing aligned with updated hints.
    - Documents Sprint 1 technical details (events, threading model, framing approach, known issues) in sprint-1-documentation.md
- ### Grant Keegan | UI & Testing Engineer
    - Implements console UI + command parsing (e.g., `/listen`, `/connect`, `/quit`) and user-friendly status output
    - Uses the event terminology (OnClientConnected) to display connection status and inbound messages\
    - Coordinates end-to-end tests with multiple app instances: `connect`, `send/receive`, `disconnect`, `invalid commands`, and `recovery behavior`
    - Validates thread safety from the user perspective (no UI freezes, clean shutdown, no duplicated prints)
    - Prepares Sprint 1 demo script and checks all checklist items are shown clearly

**Date:** 02/27/2026

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
