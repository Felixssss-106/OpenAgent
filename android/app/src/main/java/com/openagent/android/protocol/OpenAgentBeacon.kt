package com.openagent.android.protocol

/**
 * UDP presence beacon — the Android port of the .NET [LanBeaconFrame]
 * (docs/protocol.md, section 2). Format:
 * `OPENAGENT-BEACON v1|id|name|platform|version|port|ticks`.
 */
data class OpenAgentBeacon(
    val id: String,
    val name: String,
    val platform: String,
    val version: String,
    val port: Int,
    val ticks: Long,
) {
    fun encode(): String =
        listOf("OPENAGENT-BEACON v1", id, name, platform, version, port.toString(), ticks.toString())
            .joinToString("|")

    companion object {
        fun decode(line: String): OpenAgentBeacon? {
            val trimmed = line.trimEnd('\n', '\r')
            val parts = trimmed.split('|')
            if (parts.size != 7) return null
            if (parts[0] != "OPENAGENT-BEACON v1") return null
            val port = parts[5].toIntOrNull() ?: return null
            val ticks = parts[6].toLongOrNull() ?: return null
            return OpenAgentBeacon(parts[1], parts[2], parts[3], parts[4], port, ticks)
        }
    }
}
