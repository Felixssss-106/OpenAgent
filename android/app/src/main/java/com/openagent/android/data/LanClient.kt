package com.openagent.android.data

import android.os.Build
import com.openagent.android.data.model.ApprovalRequest
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.data.model.Device
import com.openagent.android.protocol.MessageEnvelope
import com.openagent.android.protocol.MessageType
import com.openagent.android.protocol.OpenAgentBeacon
import org.json.JSONObject
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.UUID
import java.util.concurrent.atomic.AtomicBoolean

/**
 * LAN discovery + messaging client (spec §9, §289; docs/protocol.md). Binds a
 * UDP socket on [port], announces an [OpenAgentBeacon] periodically, listens for
 * peer beacons and [MessageEnvelope]s, and exposes them as [StateFlow]s. Best
 * effort: if the socket cannot bind, discovery simply stays empty.
 */
class LanClient(
    private val port: Int = 47819,
    private val deviceName: String = Build.MODEL ?: "Android",
    private val version: String = Build.VERSION.RELEASE ?: "?",
    /**
     * The install-stable part of [selfId]. protocol.md promises "Stable
     * per-instance id"; a fresh UUID per launch made every restart a new device
     * on the host. The application persists one and passes it in.
     */
    private val instanceId: String = UUID.randomUUID().toString().take(8),
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
) {
    val selfId = "android:${deviceName}-${instanceId}"

    private val _devices = MutableStateFlow<List<Device>>(emptyList())
    val devices = _devices.asStateFlow()

    private val _messages = MutableStateFlow<List<ChatMessage>>(emptyList())
    val messages = _messages.asStateFlow()

    private val _approvals = MutableStateFlow<ApprovalRequest?>(null)

    /** The host's pending approval card, if one is waiting for this phone. */
    val approvals = _approvals.asStateFlow()

    private var socket: DatagramSocket? = null
    private val seen = mutableMapOf<String, Device>()
    private val peerEndpoints = mutableMapOf<String, InetAddress>()
    private val started = AtomicBoolean(false)

    fun start() {
        if (!started.compareAndSet(false, true)) return
        scope.launch { runListener() }
        scope.launch { beaconLoop() }
    }

    private fun runListener() {
        try {
            val sock = DatagramSocket(port).apply { broadcast = true }
            socket = sock
            val buf = ByteArray(4096)
            while (scope.isActive) {
                val packet = DatagramPacket(buf, buf.size)
                sock.receive(packet)
                val text = String(packet.data, 0, packet.length, Charsets.UTF_8)

                val beacon = OpenAgentBeacon.decode(text)
                if (beacon != null) {
                    if (beacon.id != selfId) {
                        synchronized(seen) {
                            seen[beacon.id] = Device(beacon.id, beacon.name, beacon.platform, beacon.version)
                            peerEndpoints[beacon.id] = packet.address
                            _devices.value = seen.values.toList()
                        }
                    }
                    continue
                }

                val env = MessageEnvelope.decode(text)
                if (env == null) continue

                // Pairing traffic belongs to the state machine, not the chat.
                if (env.type == MessageType.PAIR_REQUEST || env.type == MessageType.PAIR_CHALLENGE ||
                    env.type == MessageType.PAIR_CONFIRM || env.type == MessageType.PAIR_COMPLETE
                ) {
                    pairing?.onEnvelope(env)
                    continue
                }

                if (env.type == MessageType.APPROVAL_REQUEST) {
                    // Sealed per peer (docs/protocol.md §4.4): open with the
                    // pairing key before it can mean anything.
                    val key = pairing?.keyFor(env.from)
                    val opened =
                        if (key != null && env.cipher != null) {
                            PairingCrypto.tryOpen(key, env.nonce ?: "", env.cipher ?: "")
                        } else {
                            null
                        } ?: continue
                    val request = parseApprovalRequest(opened) ?: continue
                    _approvals.value = request
                    // The host already auto-rejects at expiry; the card follows.
                    scope.launch {
                        delay((request.expiresAt - System.currentTimeMillis()).coerceAtLeast(0))
                        if (_approvals.value?.id == request.id) _approvals.value = null
                    }
                    continue
                }

                if (env.type == MessageType.RESULT && env.cipher != null) {
                    // A sealed reply from a paired host: open it before it lands
                    // in the chat, so the user never sees ciphertext.
                    val opened = pairing?.keyFor(env.from)?.let { key ->
                        PairingCrypto.tryOpen(key, env.nonce ?: "", env.cipher ?: "")
                    }
                    val text = opened ?: continue
                    synchronized(_messages) {
                        _messages.value =
                            _messages.value + ChatMessage(env.id, ChatMessage.Direction.IN, "[${env.from}] $text")
                    }
                    continue
                }

                if (env.type == MessageType.COMMAND || env.type == MessageType.RESULT) {
                    synchronized(_messages) {
                        _messages.value =
                            _messages.value +
                            ChatMessage(env.id, ChatMessage.Direction.IN, "[${env.from}] ${env.text}")
                    }
                }
            }
        } catch (_: Exception) {
            // Socket closed or bind failed — LAN stays empty, app keeps working.
        }
    }

    private suspend fun beaconLoop() {
        while (scope.isActive) {
            runCatching {
                val beacon = OpenAgentBeacon(selfId, deviceName, "Android", version, port, System.currentTimeMillis())
                val bytes = beacon.encode().toByteArray(Charsets.UTF_8)
                socket?.send(DatagramPacket(bytes, bytes.size, InetAddress.getByName("255.255.255.255"), port))
            }
            delay(3000)
        }
    }

    /** Late-wired by [OpenAgentApplication]; null before that, pairing simply off. */
    var pairing: PairingManager? = null

    /** Sends a command envelope to [targetId]; unicast if known, else broadcast. */
    fun sendCommand(targetId: String, text: String) {
        val env = if (pairing?.isPaired(targetId) == true) {
            // Paired traffic is sealed (docs/protocol.md §4.4): the plaintext
            // never rides the wire, and the chat records what the user said.
            val key = pairing!!.keyFor(targetId)!!
            val (nonce, cipher) = PairingCrypto.seal(key, text)
            MessageEnvelope(
                type = MessageType.COMMAND,
                id = UUID.randomUUID().toString(),
                from = selfId,
                to = targetId,
                text = "",
                ts = System.currentTimeMillis(),
                nonce = nonce,
                cipher = cipher,
            )
        } else {
            MessageEnvelope(
                type = MessageType.COMMAND,
                id = UUID.randomUUID().toString(),
                from = selfId,
                to = targetId,
                text = text,
                ts = System.currentTimeMillis(),
            )
        }
        sendRaw(env)
        synchronized(_messages) {
            _messages.value = _messages.value + ChatMessage(env.id, ChatMessage.Direction.OUT, text)
        }
    }

    /** Ships a datagram to [envelope.to] — unicast to its last-seen endpoint, else broadcast. */
    fun sendRaw(envelope: MessageEnvelope) {
        val bytes = envelope.encode().toByteArray(Charsets.UTF_8)
        runCatching {
            val target = peerEndpoints[envelope.to]
            val packet =
                if (target != null) {
                    DatagramPacket(bytes, bytes.size, target, port)
                } else {
                    DatagramPacket(bytes, bytes.size, InetAddress.getByName("255.255.255.255"), port)
                }
            socket?.send(packet)
        }
    }

    /** Answers the host's approval card; the sealed resolve is the phone's vote. */
    fun resolveApproval(hostId: String, approvalId: String, approved: Boolean) {
        val key = pairing?.keyFor(hostId) ?: return
        val (nonce, cipher) = PairingCrypto.seal(
            key,
            """{"approvalId":"$approvalId","approved":$approved}""",
        )
        sendRaw(
            MessageEnvelope(
                type = MessageType.APPROVAL_RESOLVE,
                id = UUID.randomUUID().toString(),
                from = selfId,
                to = hostId,
                text = "",
                ts = System.currentTimeMillis(),
                nonce = nonce,
                cipher = cipher,
            ),
        )
        _approvals.value = null
    }

    private fun parseApprovalRequest(json: String): ApprovalRequest? = runCatching {
        val obj = JSONObject(json)
        ApprovalRequest(
            id = obj.getString("approvalId"),
            taskId = obj.optString("taskId"),
            tool = obj.optString("tool"),
            title = obj.optString("title"),
            args = obj.optString("args"),
            risk = obj.optString("risk", "Medium"),
            reversible = obj.optBoolean("reversible"),
            expiresAt = obj.optLong("expiresAtMs"),
        )
    }.getOrNull()

    fun stop() {
        runCatching { socket?.close() }
        scope.cancel()
    }
}
