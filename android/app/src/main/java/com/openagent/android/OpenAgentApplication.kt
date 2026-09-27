package com.openagent.android

import android.app.Application
import android.content.Context
import com.openagent.android.data.LanClient
import java.util.UUID

/**
 * Owns the process-wide [LanClient] so discovery and messaging survive screen
 * navigation. The client is started once and lives for the whole app (Phase 8
 * seed; pairing/trust arrive in Phase 6-7).
 */
class OpenAgentApplication : Application() {
    val lanClient = LanClient(instanceId = persistedInstanceId())

    override fun onCreate() {
        super.onCreate()
        lanClient.start()
    }

    /** protocol.md: "Stable per-instance id" — one UUID suffix per install. */
    private fun persistedInstanceId(): String {
        val prefs = getSharedPreferences("openagent", Context.MODE_PRIVATE)
        return prefs.getString("instance_id", null) ?: UUID.randomUUID().toString().take(8).also {
            prefs.edit().putString("instance_id", it).apply()
        }
    }
}
