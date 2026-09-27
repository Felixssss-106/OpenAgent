# OpenAgent cross-device wire protocol (v2)

This document is the single source of truth for how the **Windows** host
(`.NET`, `OpenAgent.Transport`) and the **Android** client (Kotlin / Jetpack
Compose) find, pair and talk to each other on a LAN. It is the Phase 6 floor of
the protocol layer (spec §60–65, §287): a presence beacon plus a JSON message
envelope carried over **UDP port 47819**. It is intentionally dependency-free so
both stacks (C# `System.Text.Json` and Kotlin `org.json` / `kotlinx.serialization`)
can implement it without a shared library.

> v2 adds **pairing and encryption** (§5). Beacons stay plaintext (they are
> advertisements); a `command` envelope is only honoured from a **paired**
> peer and travels **encrypted**. v1 envelopes remain decodable — an unpaired
> v1 `command` is refused with a plaintext `result` asking to pair.

## 1. Transport

- Protocol: **UDP**, port **47819** (well-known; overridable per deployment).
- One datagram = one message (beacon line **or** JSON envelope).
- A host that wants to be reachable binds a `UdpClient`/`DatagramSocket` to
  `0.0.0.0:47819` with broadcast enabled and **joins the bus** by both
  announcing itself (beacon) and listening.
- A host that only wants to discover may listen without announcing.

## 2. Presence beacon

A single UTF-8 line, no newline required (a trailing `\n` is tolerated):

```text
OPENAGENT-BEACON v1|id|name|platform|version|port|ticks
```

| Field     | Meaning                                                        |
|-----------|----------------------------------------------------------------|
| `id`      | Stable per-instance id, e.g. `lan:DESKTOP-AB12-c3f9` (Win) or `android:Pixel-9-1a2b` (Android). |
| `name`    | Display name (machine/model name).                            |
| `platform`| `Windows` or `Android`.                                       |
| `version` | OS / app version string.                                      |
| `port`    | The UDP port this peer listens on (usually 47819).            |
| `ticks`   | `Environment.TickCount64` (Win) / `System.currentTimeMillis()` (Android), monotonic. |

Rules:
- A peer **ignores its own beacon** (matching `id`).
- Any line that is not a well-formed beacon (wrong prefix, field count, or
  non-numeric `port`/`ticks`) is treated as a message envelope (section 3) or
  ignored.
- Peers not heard from within ~3 beacon intervals are considered offline.

## 3. Message envelope (JSON)

Every non-beacon datagram is a UTF-8 JSON object:

```json
{
  "type": "command | result | hello | pair_request | pair_challenge | pair_confirm | pair_complete | approval_request | approval_resolve",
  "id":   "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "from": "android:Pixel-9-1a2b",
  "to":   "lan:DESKTOP-AB12-c3f9",
  "text": "open notepad",
  "ts":   1716820000000,
  "pub":    "<base64 SPKI public key>",          // pair messages only
  "nonce":  "<base64 12 random bytes>",          // encrypted payloads only
  "cipher": "<base64 ciphertext || 16-byte tag>" // encrypted payloads only
}
```

| Field   | Meaning                                                        |
|---------|----------------------------------------------------------------|
| `type`  | See §3.1 and §5. `result` echoes the originating `command`'s `id`. |
| `id`    | UUID.                                                          |
| `from`  | Sender id (same namespace as the beacon `id`).                |
| `to`    | Target id, or `"*"` for broadcast.                            |
| `text`  | Plaintext payload (pairing handshake payloads, human text); empty when `cipher` is present. |
| `ts`    | Unix epoch milliseconds.                                       |
| `pub`   | Base64 X.509 SubjectPublicKeyInfo of the sender's P-256 identity key (pair messages). |
| `nonce`/`cipher` | AES-256-GCM material for an encrypted payload (§5.3); `cipher` = ciphertext followed by the 16-byte tag. |

Delivery:
- A `command` addressed to a known peer is sent **unicast** to that peer's
  last-seen endpoint; otherwise it is **broadcast** (best-effort).
- The receiver parses the envelope; a `command` raises an inbound-message event
  on the host (Windows runs it through the task pipeline; Android appends it to
  the chat). A `result` is matched by `id` and shown as the reply.

## 4. Pairing and encryption (v2)

### 4.1 Identity keys

Every peer owns one persistent P-256 key pair, generated on first run and kept
for the life of the install (Windows: SQLite `pairing` store; Android:
SharedPreferences). The public key travels as base64 SPKI. Loss of the key is
loss of identity: peers must re-pair.

### 4.2 Handshake (three datagrams + one human step)

1. **`pair_request`** (phone → host, plaintext): `text` = `{"pub":"<b64 phone pub>"}`.
2. The host derives the shared key (§4.3), computes the 6-digit PIN (§4.4),
   shows it on screen and waits for the user to accept. On acceptance it sends
   **`pair_challenge`** (host → phone, plaintext): `text` = `{"pub":"<b64 host pub>"}`.
3. The phone derives its copy of the shared key, shows a PIN input; the user
   types the code the host displayed. If it matches (§4.4) the phone sends
   **`pair_confirm`**: `text` = `{"mac":"<b64 HMAC>"}` where
   `mac = HMAC-SHA256(K, "openagent-pair-confirm" ‖ fromId ‖ toId)`.
4. The host verifies the MAC with its own key and closes with
   **`pair_complete`**: `text` = `{"ok":true}`. Both sides store
   `(peerId, peerPub, K)` and are paired.

A man-in-the-middle relaying keys produces different shared secrets on the two
sides, so the host's PIN and the phone's expectation disagree — the user sees
it and refuses. There is no out-of-band secret to steal from the wire.

### 4.3 Key derivation

```
Z = ECDH-P256(own priv, peer pub)         # raw X coordinate, zero-padded to 32 bytes
K = HKDF-SHA256(ikm = Z,
                salt = "openagent-pair-v2",
                info = "openagent/aes-256-gcm",
                length = 32)              # AES-256-GCM key
PIN = decimal(SHA256(K ‖ "openagent-pair-pin" ‖ hostId ‖ phoneId)[0..2]) mod 10^6, zero-padded to 6
MAC = HMAC-SHA256(K, "openagent-pair-confirm" ‖ senderId ‏‖ receiverId)
```

### 4.4 Encrypted application envelopes

Once paired, `command`, `result`, `approval_request` and `approval_resolve`
envelopes carry their human payload encrypted:

- `nonce`: 12 random bytes per message (never reused with the same key).
- `cipher`: AES-256-GCM over the UTF-8 payload that would have been `text`,
  tag appended, key = K of the pairing.
- `text` is empty on the wire for encrypted envelopes.

Reception rules:
- A peer with a stored key for `from` decrypts; a bad tag or a `ts` older than
  **5 minutes** (replay window) drops the datagram silently.
- A `command` from an **unpaired** sender is refused with a plaintext
  `result`: `未配对：请先在两端完成配对`. Nothing executes.
- Beacons and the four `pair_*` messages are always plaintext (they carry no
  secrets; the PIN is never on the wire).

## 5. Interop matrix

| From → To            | Mechanism                              |
|----------------------|----------------------------------------|
| Windows → Windows    | Beacon + paired encrypted envelopes over LAN. |
| Android → Windows    | Android beacon (platform `Android`) is discovered by Windows; after pairing (§4.2) Android sends encrypted `command` envelopes to the Windows host. |
| Windows → Android    | Windows sends encrypted `result`/`approval_request` envelopes to the Android peer's endpoint. |
| Android → Android    | Same protocol; two phones on the LAN see each other. |

Both sides implement `LanBeaconFrame` (beacon) and this envelope identically, so
the Windows `UdpLanTransport` and the Android `LanClient` are wire-compatible;
the v2 crypto sections are symmetric by construction (each side derives K from
its own private key and the peer's public key).
