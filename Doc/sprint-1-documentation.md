# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)

**Team Members:**
- ### Kai Fan | Team Lead / Integration & Architecture
    - Oversees overall project direction and sprint planning
    - Designs high-level architecture and component interactions
    - Handles integration between modules (UI, networking, security)
    - Manages GitHub workflow, code reviews, and merge approvals
    - Ensures sprint requirements and deadlines are met
 - ### Quang Huynh | Networking & Concurrency Engineer / Documentation
    - Implements TCP client/server communication
    - Handles multi-threaded send/receive logic
    - Builds and maintains the thread-safe message queue
    - Manages connection lifecycle (connect, disconnect, error handling)
    - Assists with performance and race-condition debugging
    - Writes and maintains sprint documentation (sprint-X-documentation.md)
- ### Uday Bista | Security & Encryption Engineer
    - Implements AES encryption/decryption for messages
    - Handles RSA key generation and secure key exchange
    - Implements message signing and signature verification
    - Ensures secure handling of keys and encrypted data
    - Documents security design and threat considerations
- ### Grant Keegan | UI & Testing 
    - Implements console UI and command handling
    - Ensures clean separation between UI and backend logic
    - Leads testing, edge-case validation, and demo preparation

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
