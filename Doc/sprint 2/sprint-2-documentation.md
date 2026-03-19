# Sprint 2 Documentation
## Secure Distributed Messenger

**Team Name:** qukg (Group 26)
 
**Team Members:**
- ### Uday Bista | Lead Security & Cryptography Engineer + Demo
    - Designs and implements full encryption pipeline (AES + RSA)
    - Implements secure key exchange protocol
    - Implements message signing and verification
    - Ensures secure handling of keys and buffers
    - Maintains threat model and security assumptions
    - Does demo implementation and test scenarios w/ Grant
    - **Primary Files:**
        - `Security/CryptoManager.cs`
        - `Security/KeyExchange.cs`
        - `Security/SignatureService.cs`

- ### Kai Fan | Architecture Support & Event Integration 
    - Updates event flow to support encrypted message lifecycle
    - Assists with integrating chat rooms into program flow
    - Maintains clean architecture boundaries between modules

    - **Primary Files:**
        - `Program.cs`
        - Event wiring across modules

- ### Quang Huynh | Documentation & Integration Engineer 
    - Documents Sprint 2 architecture, security protocol, and threat model
    - Assists with integrating encryption into networking layer
    - Ensures message framing supports encrypted payloads
    - Verifies system stability and edge-case handling

    - **Primary Files:**
        - `Docs/sprint-2-documentation.md`
        - Minor updates in:
            - `Network/Server.cs`
            - `Network/Client.cs`

- ### Grant Keegan | Chat Rooms, UI + Demo
    - Implements chat room system and command parsing
    - Integrates encryption flow into UI commands
    - Handles message routing to correct room members
    - Does demo implementation and test scenarios w/ Uday

    - **Primary Files:**
        - `Core/ChatRoomManager.cs`
        - `UI/ConsoleUI.cs` (extended)
        - `Core/CommandHandler.cs`

---

**Date:** 03/27/2026

---

## Build & Run Instructions

[Update from Sprint 1 if needed, or reference Sprint 1 documentation]

---

## Security Protocol Overview

### Encryption Protocol

#### Key Exchange Process
[Describe step-by-step how keys are exchanged when two peers connect]

1. [Step 1]
2. [Step 2]
3. [Step 3]
4. ...

#### Message Encryption
[Describe how messages are encrypted before sending]

- **Algorithm:** [e.g., AES-256-CBC]
- **Key Size:** [e.g., 256 bits]
- **IV Generation:** [How IVs are generated]

#### Message Signing
[Describe how messages are signed and verified]

- **Algorithm:** [e.g., RSA with SHA-256]
- **Key Size:** [e.g., 2048 bits]

---

## Key Management

### Key Generation
[Describe when and how keys are generated]

- **RSA Key Pair:** [When generated, how stored]
- **AES Session Key:** [When generated, lifetime]

### Key Storage
[Describe how keys are stored during runtime]

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| RSA Key Pair | | |
| AES Session Key | | |

---

## Wire Protocol

### Message Format
```
[Describe your message format, e.g.:]
[4 bytes: length][1 byte: type][payload]
```

### Message Types
| Type ID | Name | Description |
|---------|------|-------------|
| 0x01 | PUBLIC_KEY | RSA public key exchange |
| 0x02 | SESSION_KEY | Encrypted AES session key |
| 0x03 | MESSAGE | Encrypted chat message |
| 0x04 | SIGNED_MESSAGE | Signed and encrypted message |
| | | |

---

## Threat Model

### Assets Protected
- [What are you protecting? e.g., message content, user identity]

### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | AES encryption of all messages |
| Man-in-the-middle | [Your mitigation] |
| Message tampering | Digital signatures |
| Replay attacks | [Your mitigation, if any] |
| | |

### Known Limitations
[What threats are NOT addressed by your implementation?]

---

## Features Implemented

- [ ] AES encryption of messages
- [ ] RSA key pair generation
- [ ] RSA key exchange
- [ ] AES session key exchange (encrypted with RSA)
- [ ] Message signing
- [ ] Signature verification
- [ ] Multiple simultaneous conversations
- [ ] Per-conversation encryption keys

---

## Testing Performed

### Security Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Messages are encrypted on wire | Cannot read plaintext in network capture | | |
| Key exchange completes | Both peers have shared session key | | |
| Tampered message rejected | Signature verification fails | | |
| Different keys per conversation | Each peer pair has unique keys | | |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (5-7 minutes) should show:
- [ ] Two peers connecting and exchanging keys
- [ ] Sending encrypted messages
- [ ] Showing that messages are encrypted (e.g., log output)
- [ ] Demonstrating signature verification
- [ ] Showing what happens with a tampered message (if possible)
- [ ] Multiple simultaneous conversations
