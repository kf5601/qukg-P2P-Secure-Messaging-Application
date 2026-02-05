# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)

**Team Members:**
- ### Uday Bista | Security & Encryption Engineer / Message Queue
    - Defines Sprint 1 security boundaries/assumptions for the TCP layer (**plaintext in Sprint 1**, no auth yet).
    - Designs and documents **message framing/format conventions** aligned with the provided **length-prefix** examples in `HINTS.md` (what a “message” looks like on the wire).
    - Ensures safe handling practices:
        - no hardcoded secrets
        - minimize logging of private content
        - defensive input handling (size limits / sanity checks)
        - clear buffers where appropriate
    - Maintains a lightweight **threat model** for Sprint 1 (misuse cases like oversized payloads, malformed frames, disconnect spam).
    - Prepares interface contracts/extension points so encryption/authentication can be added in later sprints without rewriting networking.
    - **Implements `Core/MessageQueue.cs`**:
        - Thread-safe enqueue/dequeue (producer/consumer)
        - Uses `BlockingCollection` (or `lock`-based alternative per starter expectations)
        - Clean shutdown behavior (e.g., completion/cancellation) so networking/UI threads can exit without deadlocks.
- ### Kai Fan | Team Lead / Integration & Architecture
    - Owns overall Sprint 1 architecture and integration flow: **Console UI ↔ Program event handlers ↔ Networking (Server/Client)**
    - Implements/owns `Program.cs` lifecycle:
        - app startup/shutdown
        - thread/task creation,
        - command routing to server/client actions
        - wiring all required events (e.g., `OnClientConnected`, `OnMessageReceived`, disconnect callbacks)
    - Ensures modules communicate through **events + clean interfaces** (no tight coupling between UI and networking internals)
    - Coordinates integration of message framing decisions into networking send/receive paths (ensures everyone uses the same framing contract)
- Manages Git workflow (branches/PRs/reviews/merge discipline) and checks Sprint 1 deliverables match the rubric
- ### Quang Huynh | Networking & Concurrency Engineer / Documentation
    - Implements core TCP networking:
        - `Network/Server.cs`: `Start(port)`, accept loop, per-client receive handling, disconnect cleanup
        - `Network/Client.cs`: `ConnectAsync(host, port)`, `ReceiveAsync()`, `Send(...)`, graceful disconnect
    - Builds multi-threaded accept/receive loops using `Thread`/`Task` as expected by the starter code; prevents blocking and deadlocks on shutdown
    - Integrates **length-prefix framing** for both directions (send and receive) using Uday’s framing spec; handles partial reads and message reassembly
    - Implements robust connection lifecycle handling: exceptions, socket close detection, and cleanup without leaking threads/resources
    - Documents Sprint 1 technical details in `sprint-1-documentation.md`:
        - event flow, threading model, framing approach, error handling, known limitations
- ### Grant Keegan | UI & Testing Engineer
    - Implements console UI (`UI/ConsoleUI.cs`) and command parsing:
        - `/listen <port>`, `/connect <host> <port>`, `/quit`, plus any team-defined send/message commands.
    - Ensures user-friendly status output using event terminology:
        - prints connection/disconnection events
        - prints inbound messages
        - prints validation errors for invalid commands
    - Coordinates end-to-end tests with multiple app instances:
        - connect, send/receive, disconnect, invalid commands, recovery behavior
    - Validates thread safety from the user perspective:
        - no UI freezes
        - clean shutdown
        - no duplicate prints / missing messages under normal use
    - Prepares Sprint 1 demo script and verifies all checklist items are shown clearly

**Date:** 02/27/2026

---

## Build Instructions

### Prerequisites
- [List required software, e.g., .NET SDK version]
- [Any other dependencies]

### Building the Project
```
dotnet build
```

---

## Run Instructions

### Starting the Application
```
dotnet run
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
Sprint 1 uses background Tasks to prevent blocking the UI thread and to keep networking responsive

- **Main Thread:** 
    - Runs the console loop, reads user commands
    - Calls `Server.Start`, `Client.ConnectAsync`, `Client.Send`, `Server.Broadcas`t, `Stop`, `Disconnect`
    - Handles events from networking and prints output
- **Receive Thread:** 
    - `AcceptClientsAsync` runs in a background Task
    -  Accepts incoming TCP clients and fires `OnClientConnected(endpoint)`
    - Spawns a per client receive Task
- **Send Thread:** [Purpose]
- **Receive Thread:** [Purpose] 

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
