package com.openagent.android.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.openagent.android.OpenAgentApplication
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.data.model.Device
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn

/**
 * Holds the LAN discovery and chat state across screens. The [LanClient] is
 * process-scoped (owned by [OpenAgentApplication]) so it survives navigation.
 */
class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val lan = (application as OpenAgentApplication).lanClient

    val devices: StateFlow<List<Device>> =
        lan.devices.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    val messages: StateFlow<List<ChatMessage>> =
        lan.messages.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    fun sendCommand(targetId: String, text: String) = lan.sendCommand(targetId, text)
}
