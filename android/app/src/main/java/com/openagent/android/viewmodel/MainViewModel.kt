package com.openagent.android.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.openagent.android.OpenAgentApplication
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.data.model.Device
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn

/**
 * Holds the LAN discovery and chat state across screens. The [LanClient] is
 * process-scoped (owned by [OpenAgentApplication]) so it survives navigation.
 * The view model itself is activity-scoped: MainActivity creates one instance
 * and hands it to every screen, so the device a user picks on 设备 is the
 * target the Agent page sends to.
 */
class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val lan = (application as OpenAgentApplication).lanClient

    val devices: StateFlow<List<Device>> =
        lan.devices.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    val messages: StateFlow<List<ChatMessage>> =
        lan.messages.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    private val _selectedTarget = MutableStateFlow<Device?>(null)

    /** The device the user picked on 设备; null until they do (agent auto-picks). */
    val selectedTarget: StateFlow<Device?> = _selectedTarget.asStateFlow()

    fun selectTarget(device: Device) {
        _selectedTarget.value = device
    }

    fun sendCommand(targetId: String, text: String) = lan.sendCommand(targetId, text)
}
