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
        - `Security/AesEncryption.cs`
        - `Security/RsaEncryption.cs`
        - `Security/KeyExchange.cs`
        - `Security/MessageSigner.cs`

- ### Kai Fan | Architecture Support & Event Integration 
    - Updates event flow to support encrypted message lifecycle
    - Assists with integrating chat rooms into program flow
    - Maintains clean architecture boundaries between modules

    - **Primary Files:**
        - `Program.cs` (command handling + wiring for encrypted send/receive)
        - `Core/Message.cs` (message schema for encrypted payloads/signatures)
        - Integration touchpoints in `Network/Client.cs` and `Network/Server.cs`

- ### Quang Huynh | Documentation & Integration Engineer 
    - Documents Sprint 2 architecture, security protocol, and threat model
    - Assists with integrating encryption into networking layer
    - Ensures message framing supports encrypted payloads
    - Verifies system stability and edge-case handling

    - **Primary Files:**
        - `Doc/sprint 2/sprint-2-documentation.md`
        - Minor updates in:
            - `Network/Server.cs`
            - `Network/Client.cs`

- ### Grant Keegan | Chat Rooms, UI + Demo
    - Implements chat room system and command parsing
    - Integrates encryption flow into UI commands
    - Handles message routing to correct room members
    - Does demo implementation and test scenarios w/ Uday

    - **Primary Files:**
        - `UI/ConsoleUI.cs`
        - `Program.cs` (room command dispatch)
        - `Network/Server.cs` (room membership + targeted broadcast)

---

**Date:** 03/27/2026

---

## Build & Run Instructions

### Prerequisites
- .NET 9.0 SDK or later
- No additional external dependencies

### Build
```bash
dotnet build SecureMessenger.csproj
```

### Run
```bash
dotnet run --project SecureMessenger.csproj
```

### Notes
- The current console command set includes `/listen <port>`, `/connect <host> <port> <name>`, `/peers`, `/room create <name>`, `/room join <name>`, `/room leave`, `/room list`, `/trust <name>`, `/keyinfo`, `/help`, and `/quit`
- Sprint 2 security helper classes in `Security/*` are wired into the live handshake and encrypted message path
- Chat-room commands are available through `Program.cs` and `UI/ConsoleUI.cs`

---

## Security Protocol Overview

### Encryption Protocol

#### Key Exchange Process
The Sprint 2 handshake is implemented in `Security/KeyExchange.cs` as a state machine and is used by `Network/Client.cs` and `Network/Server.cs` to bootstrap an AES session key per TCP connection.

1. When a secure connection is being established, each side creates a `KeyExchange` instance, which creates a fresh RSA-2048 key pair in memory.
2. When a connection is established, the initiator sends its RSA public key in a `Message` whose `Type` is `MessageType.KeyExchange`.
3. The responder stores that public key and returns its own RSA public key in another `MessageType.KeyExchange` message.
4. After the initiator has the responder's public key, it generates a random 32-byte AES session key with `AesEncryption.GenerateKey()`.
5. The initiator encrypts that AES key with the responder's RSA public key using OAEP-SHA256 and sends the result as a `MessageType.SessionKey` message.
6. The responder decrypts the encrypted session key with its RSA private key and stores the recovered AES key as the session key for that connection.
7. Once both sides hold the same AES session key, subsequent text traffic is encrypted with AES before transmission and decrypted on receipt.

Current integration status: the handshake is live in `Client.PerformKeyExchangeAsync()` and `Server.ReceiveFromClientAsync()`. After the handshake completes, text messages are serialized through `EncryptedContent` instead of raw `Content`.

#### Message Encryption
For Sprint 2, message confidentiality is designed around per-connection AES session keys.

- **Algorithm:** AES-256-CBC via `System.Security.Cryptography.Aes`
- **Key Size:** 256 bits (32 bytes)
- **IV Generation:** A fresh random 16-byte IV is generated for every encrypted message with `aes.GenerateIV()`
- **Ciphertext Format:** `[IV (16 bytes)] [ciphertext (N bytes)]`
- **Per-recipient behavior:** The `Message` model already includes `TargetPeerId`, which can be used to route a separately encrypted copy to a specific peer instead of broadcasting a single plaintext/shared payload

#### Message Signing
Message authenticity and tamper detection are implemented by `Security/MessageSigner.cs`.

- **Algorithm:** RSA with SHA-256
- **Padding:** PKCS#1 v1.5 via `RSASignaturePadding.Pkcs1`
- **Key Size:** 2048 bits
- **Signing flow:** `Program.SendMessage()` signs the UTF-8 bytes of plaintext before send and stores the result in `Message.Signature` after a peer has been manually trusted with `/trust`
- **Verification flow:** `MessageSigner.VerifyData()` can verify a signature with the sender's public key, but that helper is not yet invoked in the live receive path
- **Failure behavior:** Invalid or unparsable signatures return `false` and log a warning or verification error

---

## Key Management

### Key Generation
The design uses runtime-only keys rather than persisted credentials.

- **Handshake RSA Key Pair:** Generated when a `KeyExchange` / `RsaEncryption` instance is created for a connection. The current implementation uses `RSA.Create(2048)`.
- **Signing RSA Key Pair:** Generated once in `Program.cs` as `_localRsa` and used by `MessageSigner`.
- **AES Session Key:** Generated by the connection initiator after public-key exchange with `AesEncryption.GenerateKey()`. One AES key is established per TCP connection.

### Key Storage
Keys are stored only in process memory in the current implementation.

- Handshake RSA private keys stay inside the `RSA` object owned by `RsaEncryption`
- The signing private key stays inside `_localRsa` in `Program.cs`
- Exported public keys are serialized into `Message.PublicKey` for exchange
- AES session keys are stored as `byte[]` in `KeyExchange.SessionKey` and wrapped by `AesEncryption`
- No keys are written to disk
- No long-term key trust store or certificate chain is used

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| Handshake RSA Key Pair | `KeyExchange` construction during connection setup | Connection teardown or process exit |
| Signing RSA Key Pair | Application startup in `Program.cs` | Process exit or restart |
| AES Session Key | After public-key exchange for a specific connection | Connection teardown, process exit, or explicit re-key |

---

## Wire Protocol

### Message Format
```
[4 bytes: payload length, little-endian Int32]
[UTF-8 JSON payload of SecureMessenger.Core.Message]
```

The network layer in both `Network/Client.cs` and `Network/Server.cs` uses length-prefix framing from Sprint 1. The JSON payload is a serialized `Message` object. `byte[]` properties are JSON-encoded as Base64 strings by `System.Text.Json`.

Relevant `Message` fields for Sprint 2:
- `Id`: unique message identifier
- `Sender`: display name / sender identifier
- `Timestamp`: local send time
- `Type`: protocol selector (`Text`, `KeyExchange`, `SessionKey`, ...)
- `Content`: plaintext content before encryption and after decryption for local display
- `EncryptedContent`: encrypted bytes for Sprint 2 secure payloads
- `Signature`: sender signature bytes
- `PublicKey`: sender RSA public key bytes
- `TargetPeerId`: optional routing hint for directed delivery

### Message Types
| Type ID | Name | Description |
|---------|------|-------------|
| 0 | TEXT | Regular chat content |
| 1 | KEY_EXCHANGE | RSA public key exchange |
| 2 | SESSION_KEY | AES session key encrypted with RSA |
| 3 | HEARTBEAT | Reserved for Sprint 3 |
| 4 | PEER_DISCOVERY | Reserved for Sprint 3 |

### Concurrency / Queue Notes
`Core/MessageQueue.cs` is already implemented and is the right place to decouple UI input from encrypted network fan-out.

- Without end-to-end encryption, the server can broadcast one shared message object.
- With per-peer AES session keys, one logical outbound message becomes N separately encrypted payloads, one per recipient.
- A queue keeps that work off the UI thread and avoids multiple connection threads racing while they encrypt and write concurrently.
- On the receive side, multiple sockets can enqueue inbound encrypted messages so verification, decryption, and display happen in a controlled order.
- `TargetPeerId` is useful for directed delivery because routing metadata can stay in the outer message while `EncryptedContent` protects the chat body.

---

## Threat Model

### Assets Protected
- Message confidentiality between connected peers
- Message integrity and sender authenticity
- RSA private keys and per-connection AES session keys
- Basic network stability against malformed or oversized frames

### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | Per-connection AES encryption is active on the live send/receive path after the RSA handshake completes |
| Man-in-the-middle | Not fully mitigated in the current design because public keys are exchanged without an external trust anchor, certificate, or fingerprint verification step |
| Message tampering | Signature support exists, but receive-path verification is only partially integrated |
| Replay attacks | Not currently mitigated; there is no nonce, sequence number, or timestamp freshness validation in the receive path |
| Malformed / oversized payloads | Length-prefix validation rejects non-positive or very large payload sizes; JSON parse failures are caught and skipped |
| Concurrent stream writes | Client sends are serialized with `SemaphoreSlim`; server access to `_clients` is protected with `lock` |

### Known Limitations
- No certificate validation, fingerprint pinning, or trust-on-first-use store is implemented, so active MITM remains possible
- No replay protection or duplicate suppression is implemented
- Signature verification helper exists, but `Program.cs` does not yet enforce `MessageSigner.VerifyData()` before displaying incoming messages
- `MessageQueue` exists but is not yet integrated into the live send/receive pipeline
- `TargetPeerId` is used for targeted protocol delivery, but normal chat routing still relies mainly on broadcast and room membership
- Each app instance still maintains a single outgoing `Client` connection even though a listening server can manage multiple inbound peers

---

## Features Implemented

- [x] AES encryption of messages on the live network path
- [x] RSA key pair generation
- [x] RSA key exchange helper/state machine
- [x] AES session key exchange helper (encrypted with RSA)
- [x] Message signing helper
- [x] Signature verification helper
- [x] Multiple simultaneous conversations
- [x] Per-connection encryption keys fully integrated into networking

---

## Testing Performed

### Security Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Project builds | `dotnet build SecureMessenger.csproj` succeeds | Build succeeds with 0 warnings and 0 errors on 03/27/2026 | Pass |
| Messages are encrypted in transit | Outbound text is carried in `EncryptedContent` instead of plaintext | `Client.SendAsync()` and `Server.Broadcast*()` encrypt text with AES after handshake, and the receive loops decrypt before display | Pass |
| Key exchange completes | Both peers have shared session key | `Client.PerformKeyExchangeAsync()` and `Server.ReceiveFromClientAsync()` exchange RSA public keys, wrap the AES session key, and initialize `AesEncryption` on both sides | Pass |
| Tampered message rejected | Signature verification fails | `MessageSigner.VerifyData()` rejects invalid signatures at helper level, but end-to-end receive-path enforcement is not yet wired | Partial |
| Different keys per connection | Each peer session has a unique AES key | Server stores separate `AesEncryption` instances in `_aesSessions` per `TcpClient`, and each handshake generates a fresh session key | Pass |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| Signature verification not enforced | Outbound messages can carry `Message.Signature`, but the live receive path does not call `MessageSigner.VerifyData()` before display | Verify `Signature` against the sender public key before accepting and displaying inbound text |
| No authenticated key distribution | Public keys are exchanged directly over the same channel with no fingerprint validation or certificate chain | Use manual fingerprint verification or add trusted-key persistence |
| No replay protection | Messages have timestamps and GUIDs but they are not enforced for freshness/uniqueness checks | Add nonce/sequence tracking per session |
| Single outbound connection per client | `Client.cs` still manages one outgoing server connection at a time, even though a listening peer can handle multiple inbound clients | Run multiple app instances or extend peer management in Sprint 3 |
| Queue not integrated | `Core/MessageQueue.cs` exists but outbound/inbound encrypted work is still processed inline | Use the queue to buffer per-recipient encryption and serialized decrypt/display work |

---

## Video Demo Checklist

Your demo video (5-7 minutes) should show:
- [x] Two peers connecting and exchanging keys
- [x] Sending encrypted messages
- [x] Showing that messages are encrypted (e.g., log output)
- [x] Demonstrating signature verification
- [x] Showing what happens with a tampered message (if possible)
- [x] Multiple simultaneous conversations

