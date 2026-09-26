package com.openagent.android.data.model

/** A peer discovered on the LAN via the OpenAgent beacon (docs/protocol.md). */
data class Device(
    val id: String,
    val name: String,
    val platform: String,
    val version: String,
    val lastSeen: Long = System.currentTimeMillis(),
)
