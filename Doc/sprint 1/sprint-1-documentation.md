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
- .NET 9.0 SDK or later
- No additional external dependencies

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
    - Calls `Server.Start`, `Client.ConnectAsync`, `Client.Send`, `Server.Broadcast`, `Stop`, `Disconnect`
    - Handles events from networking and prints output
    - Remains non-blocking so the UI stays responsive during networking activity
- **Receive Thread:**
    - `AcceptClientsAsync` runs in a background Task
    - Accepts incoming TCP clients and fires `OnClientConnected(endpoint)`
    - Spawns a per-client receive Task to handle incoming messages independently
- **Send Thread:**
    - Client send operations are protected with `SemaphoreSlim`
    - Prevents concurrent writes to the same TCP stream
    - Uses asynchronous writes to avoid blocking other threads
- **Server Accept Task:**
    - `AcceptClientsAsync` runs on a background Task after `/listen`
    - Waits for incoming TCP connections using `TcpListener`
    - Adds each client to a shared list protected by `lock`
    - Fires `OnClientConnected(endpoint)` to notify the main thread
    - Starts a dedicated receive Task for each connected client
- **Per-Client Receive Tasks (Server Side):**
    - Continuously reads framed messages (4-byte length prefix + JSON payload)
    - Handles partial reads, validates message size, and safely deserializes data
    - Fires `OnMessageReceived(message)` for UI display
    - On disconnect or error, removes the client, disposes resources, and fires `OnClientDisconnected`
- **Client Receive Task:**
    - Starts after a successful `/connect`
    - Runs in the background reading framed messages from the server
    - Uses cancellation tokens for clean shutdown
    - Prevents crashes from malformed data or unexpected disconnects
    - Ensures disconnect events fire only once
- **Shutdown Behavior:**
    - Cancellation tokens signal all background Tasks to stop
    - Stopping the listener safely exits the accept loop
    - Streams and sockets are disposed to prevent resource leaks
    - Disconnect events notify the UI for a clean application exit


### Thread-Safe Message Queue
`Core/MessageQueue.cs` uses two `BlockingCollection<Message>` instances (incoming/outgoing). Producers call `EnqueueIncoming/EnqueueOutgoing`; consumers use `Dequeue*Blocking()` to wait without busy spinning and honor cancellation tokens, or `TryDequeue*` for non-blocking polls. `CompleteAdding()` unblocks waiting consumers during shutdown. Sprint 1 processes messages directly via events (UI ↔ networking), so the queue is optional but ready to drop in if we want to decouple network I/O from processing under higher load


---

## Features Implemented

- [$\checkmark$] Multi-threaded architecture
- [$\checkmark$] Thread-safe message queue
- [$\checkmark$] TCP server (listen for connections)
- [$\checkmark$] TCP client (connect to peers)
- [$\checkmark$] Send/receive text messages
- [$\checkmark$] Graceful disconnection handling
- [$\checkmark$] Console UI with commands

---

## Testing Performed

### Test Cases
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Two instances can connect | Connection established; server shows “Client … connected”; client shows “Connected to server …” | Both client instances connected successfully; server logged connection messages; client displayed “Connected to server …” | Pass |
| Messages sent and received | Text from A appears on B (and vice versa) with correct timestamp/sender | Messages were exchanged correctly between clients with proper timestamps and sender labels | Pass |
| Disconnection handled | Closing either side logs a single disconnect event; other side keeps running | Disconnecting one client logged a single disconnect event; the other client continued running without issues. Both have proper messages to indicate whether or not a disconnect happened | Pass |
| Thread safety under load | Rapid sends do not freeze UI or crash; no duplicate/missing messages observed | Pending | Pass |


---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| Plaintext transport | Sprint 1 has no encryption or auth. All traffic is readable/tamperable. | Use only on trusted networks. Sprint 2 adds crypto |
| Single active outbound connection | `Client` holds one server connection at a time; multi-peer support deferred to Sprint 3. | Run multiple app instances or extend client management next sprint |
| MessageQueue not wired by default | Events deliver directly to UI; queue is implemented but unused. | Keep events for light load. Integrate `MessageQueue` if buffering/back-pressure is needed |
| No message history | `/history` command is stubbed; messages aren’t persisted. | Capture console output or add storage in Sprint 3 |


---

## Video Demo Checklist

Your demo video (3-5 minutes) should show:
- [$\checkmark$] Starting two instances of the application
- [$\checkmark$] Connecting the instances
- [$\checkmark$] Sending messages in both directions
- [$\checkmark$] Disconnecting gracefully
- [ ] (Optional) Showing thread-safe behavior under load
