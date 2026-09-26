# OpenAgent Android Client (v1.0.0 seed)

The Android companion for the Windows AI cross-device control center. It
implements Phase 8 of the spec (spec §289) as a **seed**: real UDP LAN
discovery and command chat against the Windows host over the OpenAgent v1 wire
protocol (docs/protocol.md).

## Stack

- Kotlin 2.0.21
- Jetpack Compose + Material 3
- Kotlin Coroutines
- Framework `org.json` for envelope parsing (no extra serialization dependency)
- UDP DatagramSocket (port 47819)

## Build

Requires Android Studio Ladybug (2024.2.1+) with SDK 35 and the Android Gradle
Plugin 8.7.3. Open `android/` as a project in Android Studio and build:

```bash
cd android
./gradlew :app:assembleDebug   # or assembleRelease for a signed APK seed
```

> This sandbox does **not** ship the Android SDK, so the project is
> source-verified only here. It is written to compile in Android Studio.

## Features (seed)

- **Discovery**: Announces an `OPENAGENT-BEACON v1` on UDP port 47819 and listens
  for Windows hosts (or other Android peers) on the same LAN.
- **Devices screen**: Lists discovered peers with platform/version; tap to open
  the chat.
- **Agent chat**: Type a command (e.g. "open notepad"), send it as a JSON
  `MessageEnvelope` to the selected Windows host, and see the reply.
- **Settings**: Shows the device id, transport port, and system info.

## Wire protocol

The Android client speaks the exact same protocol as the .NET
`OpenAgent.Transport` layer:

- Beacon: `OPENAGENT-BEACON v1|id|name|platform|version|port|ticks`
- Envelope: `{"type":"command","id":"...","from":"...","to":"...","text":"...","ts":...}`

Both sides are byte-for-byte compatible.
