package com.openagent.android.protocol

import org.json.JSONObject

/**
 * JSON message envelope carried over UDP (docs/protocol.md, section 3) — the
 * Android port of the .NET `LanMessageEnvelope`. Uses the framework `org.json`
 * so there is no extra serialization dependency. v2 adds the pairing types and
 * the optional pub/nonce/cipher fields (§4).
 */
enum class MessageType {
    COMMAND, RESULT, HELLO,
    PAIR_REQUEST, PAIR_CHALLENGE, PAIR_CONFIRM, PAIR_COMPLETE,
    APPROVAL_REQUEST, APPROVAL_RESOLVE,
}

data class MessageEnvelope(
    val type: MessageType,
    val id: String,
    val from: String,
    val to: String,
    val text: String,
    val ts: Long,
    val pub: String? = null,
    val nonce: String? = null,
    val cipher: String? = null,
) {
    fun encode(): String =
        JSONObject()
            .apply {
                put("type", type.name.lowercase())
                put("id", id)
                put("from", from)
                put("to", to)
                put("text", text)
                put("ts", ts)
                pub?.let { put("pub", it) }
                nonce?.let { put("nonce", it) }
                cipher?.let { put("cipher", it) }
            }.toString()

    companion object {
        fun decode(json: String): MessageEnvelope? =
            runCatching {
                val obj = JSONObject(json)
                val type =
                    when (obj.optString("type")) {
                        "command" -> MessageType.COMMAND
                        "result" -> MessageType.RESULT
                        "hello" -> MessageType.HELLO
                        "pair_request" -> MessageType.PAIR_REQUEST
                        "pair_challenge" -> MessageType.PAIR_CHALLENGE
                        "pair_confirm" -> MessageType.PAIR_CONFIRM
                        "pair_complete" -> MessageType.PAIR_COMPLETE
                        "approval_request" -> MessageType.APPROVAL_REQUEST
                        "approval_resolve" -> MessageType.APPROVAL_RESOLVE
                        else -> return null
                    }
                val id = obj.optString("id").takeIf { it.isNotBlank() } ?: return null
                val from = obj.optString("from").takeIf { it.isNotBlank() } ?: return null
                val to = obj.optString("to").takeIf { it.isNotBlank() } ?: return null
                MessageEnvelope(
                    type,
                    id,
                    from,
                    to,
                    obj.optString("text", ""),
                    obj.optLong("ts", 0L),
                    obj.optString("pub", "").takeIf { it.isNotEmpty() },
                    obj.optString("nonce", "").takeIf { it.isNotEmpty() },
                    obj.optString("cipher", "").takeIf { it.isNotEmpty() },
                )
            }.getOrNull()
    }
}
