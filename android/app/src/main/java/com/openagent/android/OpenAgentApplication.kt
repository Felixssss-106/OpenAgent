package com.openagent.android

import android.app.Application
import com.openagent.android.data.LanClient

/**
 * Owns the process-wide [LanClient] so discovery and messaging survive screen
 * navigation. The client is started once and lives for the whole app (Phase 8
 * seed; pairing/trust arrive in Phase 6-7).
 */
class OpenAgentApplication : Application() {
    val lanClient = LanClient()

    override fun onCreate() {
        super.onCreate()
        lanClient.start()
    }
}
