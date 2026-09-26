package com.openagent.android.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavController
import com.openagent.android.data.model.Device
import com.openagent.android.viewmodel.MainViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DevicesScreen(navController: NavController, vm: MainViewModel = viewModel()) {
    val devices by vm.devices.collectAsStateWithLifecycle()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("设备") },
                actions = {
                    IconButton(onClick = { navController.navigate("settings") }) {
                        Icon(Icons.Default.Settings, contentDescription = "设置")
                    }
                }
            )
        },
        floatingActionButton = {
            // Reserved for manual-add or refresh seed.
            ExtendedFloatingActionButton(
                onClick = { /* manual add seed */ },
                icon = { Icon(Icons.Default.Add, contentDescription = null) },
                text = { Text("添加") }
            )
        }
    ) { padding ->
        if (devices.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding),
                contentAlignment = Alignment.Center
            ) {
                Text("未发现设备\n确保与 Windows 端在同一局域网", style = MaterialTheme.typography.bodyLarge)
            }
        } else {
            LazyColumn(modifier = Modifier.padding(padding)) {
                items(devices, key = { it.id }) { device ->
                    DeviceCard(device = device, onClick = {
                        navController.navigate("chat/${device.id}")
                    })
                }
            }
        }
    }
}

@Composable
private fun DeviceCard(device: Device, onClick: () -> Unit) {
    Card(
        onClick = onClick,
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 8.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(device.name, style = MaterialTheme.typography.titleMedium)
            Text(
                "${device.platform} · ${device.version}",
                style = MaterialTheme.typography.bodyMedium
            )
        }
    }
}
