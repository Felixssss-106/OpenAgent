package com.openagent.android.data.model

/** A command/result line shown in the agent chat. */
data class ChatMessage(
    val id: String,
    val direction: Direction,
    val text: String,
    val ts: Long = System.currentTimeMillis(),
) {
    enum class Direction { OUT, IN }
}
