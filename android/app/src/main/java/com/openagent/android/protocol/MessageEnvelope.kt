package com.openagent.android.protocol

import org.json.JSONObject

/**
 * JSON message envelope carried over UDP (docs/protocol.md, section 3) — the
 * Android port of the .NET [LanMessageEnvelope]. Uses the framework `org.json`
 * so there is no extra serialization dependency.
 */
enum class MessageType { COMMAND, RESULT, HELLO }

data class MessageEnvelope(
    val type: MessageType,
    val id: String,
    val from: String,
    val to: String,
    val text: String,
    val ts: Long,
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
                        else -> return null
                    }
                val id = obj.optString("id").takeIf { it.isNotBlank() } ?: return null
                val from = obj.optString("from").takeIf { it.isNotBlank() } ?: return null
                val to = obj.optString("to").takeIf { it.isNotBlank() } ?: return null
                MessageEnvelope(type, id, from, to, obj.optString("text", ""), obj.optLong("ts", 0L))
            }.getOrNull()
    }
}
