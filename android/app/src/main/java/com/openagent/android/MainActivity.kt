package com.openagent.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.openagent.android.ui.screens.AgentChatScreen
import com.openagent.android.ui.screens.DevicesScreen
import com.openagent.android.ui.screens.SettingsScreen
import com.openagent.android.ui.theme.OpenAgentTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            OpenAgentTheme {
                OpenAgentApp()
            }
        }
    }
}

@Composable
fun OpenAgentApp() {
    val navController = rememberNavController()
    Scaffold { innerPadding ->
        NavHost(
            navController = navController,
            startDestination = "devices",
            modifier = Modifier.padding(innerPadding)
        ) {
            composable("devices") { DevicesScreen(navController) }
            composable("chat/{deviceId}") { backStackEntry ->
                val deviceId = backStackEntry.arguments?.getString("deviceId") ?: ""
                AgentChatScreen(deviceId = deviceId)
            }
            composable("settings") { SettingsScreen() }
        }
    }
}
