# OpenAgent cross-device wire protocol (v1)

This document is the single source of truth for how the **Windows** host
(`.NET`, `OpenAgent.Transport`) and the **Android** client (Kotlin / Jetpack
Compose) find and talk to each other on a LAN. It is the Phase 6 floor of the
protocol layer (spec §60–65, §287): a presence beacon plus a JSON message
envelope carried over **UDP port 47819**. It is intentionally dependency-free so
both stacks (C# `System.Text.Json` and Kotlin `org.json` / `kotlinx.serialization`)
can implement it without a shared library.

> Everything here is **plaintext and unpaired**. Phase 6–7 adds mDNS, the
> Cloudflare Relay, pairing, trust and encryption. Do not send secrets over v1.

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
  "type": "command" | "result" | "hello",
  "id":   "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "from": "android:Pixel-9-1a2b",
  "to":   "lan:DESKTOP-AB12-c3f9",
  "text": "open notepad",
  "ts":   1716820000000
}
```

| Field   | Meaning                                                        |
|---------|----------------------------------------------------------------|
| `type`  | `command` (a request), `result` (a reply), `hello` (presence handshake beyond the beacon). |
| `id`    | UUID; the `result` echoes the `command`'s `id`.               |
| `from`  | Sender id (same namespace as the beacon `id`).                |
| `to`    | Target id, or `"*"` for broadcast.                            |
| `text`  | Human-readable payload (command text / result text).         |
| `ts`    | Unix epoch milliseconds.                                      |

Delivery:
- A `command` addressed to a known peer is sent **unicast** to that peer's
  last-seen endpoint; otherwise it is **broadcast** (best-effort).
- The receiver parses the envelope; a `command` raises an inbound-message event
  on the host (Windows logs it and surfaces it in the task list; Android appends
  it to the chat). A `result` is matched by `id` and shown as the reply.

## 4. Interop matrix (v1.0.0)

| From → To            | Mechanism                              |
|----------------------|----------------------------------------|
| Windows → Windows    | Beacon + command envelope over LAN.   |
| Android → Windows    | Android beacon (platform `Android`) is discovered by Windows; Android sends `command` envelopes to the Windows host. |
| Windows → Android    | Windows sends `command`/`result` envelopes to the Android peer's endpoint. |
| Android → Android    | Same protocol; two phones on the LAN see each other. |

Both sides implement `LanBeaconFrame` (beacon) and this envelope identically, so
the Windows `UdpLanTransport` and the Android `LanClient` are wire-compatible.
