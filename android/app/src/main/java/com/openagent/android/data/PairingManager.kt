package com.openagent.android.data

import android.content.Context
import com.openagent.android.data.model.Device
import com.openagent.android.protocol.MessageEnvelope
import com.openagent.android.protocol.MessageType
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONObject
import java.util.UUID

/**
 * The phone side of the v2 pairing handshake (docs/protocol.md §4.2). The phone
 * initiates with pair_request, the host shows a PIN derived from the shared
 * secret, the user types it here, and only a matching code lets pair_confirm
 * fly — a man-in-the-middle relaying keys cannot make the two screens agree.
 * The pairing persists in SharedPreferences next to the identity key.
 */
class PairingManager(
    context: Context,
    private val selfId: String,
    private val send: (MessageEnvelope) -> Unit,
) {
    /** A host whose challenge is waiting for the user's PIN input. */
    data class Challenge(val host: Device, val expectedPin: String)

    private val prefs = context.getSharedPreferences("openagent", Context.MODE_PRIVATE)

    private val _challenge = MutableStateFlow<Challenge?>(null)
    val challenge: StateFlow<Challenge?> = _challenge.asStateFlow()

    private val _pairedIds = MutableStateFlow<Set<String>>(loadPairedIds())
    val pairedIds: StateFlow<Set<String>> = _pairedIds.asStateFlow()

    private var pendingHostId: String? = null
    private var pendingKey: ByteArray? = null
    private var pendingPeerPub: String? = null

    init {
        ensureIdentity()
    }

    /** Generates the install's identity key on first run; later calls keep it. */
    private fun ensureIdentity() {
        if (prefs.getString(PREF_IDENTITY_PRIVATE, null) == null) {
            val (priv, pub) = PairingCrypto.generateIdentity()
            prefs.edit()
                .putString(PREF_IDENTITY_PRIVATE, priv)
                .putString(PREF_IDENTITY_PUBLIC, pub)
                .apply()
        }
    }

    fun isPaired(hostId: String): Boolean = hostId in _pairedIds.value

    fun keyFor(hostId: String): ByteArray? {
        val stored = prefs.getString(pairKey(hostId), null) ?: return null
        return Base64.decode(stored)
    }

    /** Step 1: offer our identity key to [hostId]. The host answers with a PIN on its screen. */
    fun startPairing(hostId: String) {
        val pub = prefs.getString(PREF_IDENTITY_PUBLIC, null) ?: return
        pendingHostId = hostId
        send(
            MessageEnvelope(
                type = MessageType.PAIR_REQUEST,
                id = UUID.randomUUID().toString(),
                from = selfId,
                to = hostId,
                text = """{"pub":"$pub"}""",
                ts = System.currentTimeMillis(),
            ),
        )
    }

    fun cancelPairing() {
        pendingHostId = null
        pendingKey = null
        pendingPeerPub = null
        _challenge.value = null
    }

    /** Consumes a pairing envelope from the listener; returns true when handled. */
    fun onEnvelope(envelope: MessageEnvelope) {
        when (envelope.type) {
            MessageType.PAIR_CHALLENGE -> onPairChallenge(envelope)
            MessageType.PAIR_COMPLETE -> onPairComplete(envelope)
            else -> Unit
        }
    }

    private fun onPairChallenge(envelope: MessageEnvelope) {
        val pub = runCatching { JSONObject(envelope.text).optString("pub") }.getOrNull()?.takeIf { it.isNotEmpty() }
            ?: return
        val priv = prefs.getString(PREF_IDENTITY_PRIVATE, null) ?: return

        val key = runCatching { PairingCrypto.deriveSharedKey(priv, pub) }.getOrNull() ?: return
        val pin = PairingCrypto.pinFor(key, envelope.from, selfId)
        pendingHostId = envelope.from
        pendingKey = key
        pendingPeerPub = pub
        // The host's display name arrives via its beacon; the challenge only
        // knows the id, so the dialog leads with the code, not the name.
        _challenge.value = Challenge(Device(envelope.from, envelope.from, "Windows", ""), pin)
    }

    /** The user typed [code]; matching the expectation proves both sides derived the same key. */
    fun submitPin(code: String) {
        val challenge = _challenge.value ?: return
        val key = pendingKey ?: return
        if (code != challenge.expectedPin) {
            return
        }

        val mac = PairingCrypto.confirmMac(key, selfId, challenge.host.id)
        send(
            MessageEnvelope(
                type = MessageType.PAIR_CONFIRM,
                id = UUID.randomUUID().toString(),
                from = selfId,
                to = challenge.host.id,
                text = """{"mac":"${Base64.encode(mac)}"}""",
                ts = System.currentTimeMillis(),
            ),
        )
    }

    private fun onPairComplete(envelope: MessageEnvelope) {
        val key = pendingKey ?: return
        if (envelope.from != pendingHostId) return
        val ok = runCatching { JSONObject(envelope.text).optBoolean("ok") }.getOrDefault(false)
        if (!ok) return

        prefs.edit()
            .putString(pairKey(envelope.from), Base64.encode(key))
            .putString(hostPubKey(envelope.from), pendingPeerPub ?: return)
            .apply()
        _pairedIds.value = loadPairedIds()
        pendingKey = null
        pendingPeerPub = null
        pendingHostId = null
        _challenge.value = null
    }

    private fun loadPairedIds(): Set<String> =
        prefs.all.keys.filter { it.startsWith(PREF_PAIR_PREFIX) }
            .map { it.removePrefix(PREF_PAIR_PREFIX) }.toSet()

    private fun pairKey(hostId: String) = "$PREF_PAIR_PREFIX$hostId"

    private fun hostPubKey(hostId: String) = "$PREF_HOST_PUB_PREFIX$hostId"

    private companion object {
        const val PREF_IDENTITY_PRIVATE = "identity_private"
        const val PREF_IDENTITY_PUBLIC = "identity_public"
        const val PREF_PAIR_PREFIX = "pairing_"
        const val PREF_HOST_PUB_PREFIX = "pairing_pub_"
    }
}
