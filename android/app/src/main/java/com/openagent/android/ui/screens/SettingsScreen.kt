package com.openagent.android.ui.screens

import android.os.Build
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.openagent.android.OpenAgentApplication
import com.openagent.android.viewmodel.MainViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(vm: MainViewModel = viewModel()) {
    val app = LocalContext.current.applicationContext as OpenAgentApplication

    Scaffold(
        topBar = {
            TopAppBar(title = { Text("设置") })
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text("OpenAgent Android v1.0.0", style = MaterialTheme.typography.headlineSmall)
            HorizontalDivider()
            Text("设备标识", style = MaterialTheme.typography.titleMedium)
            Text(app.lanClient.selfId, style = MaterialTheme.typography.bodyMedium)
            HorizontalDivider()
            Text("传输", style = MaterialTheme.typography.titleMedium)
            Text("端口: 47819 (UDP)")
            Text("协议: OpenAgent v1 (docs/protocol.md)")
            HorizontalDivider()
            Text("系统", style = MaterialTheme.typography.titleMedium)
            Text("Android ${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})")
            Text("机型: ${Build.MODEL}")
        }
    }
}
