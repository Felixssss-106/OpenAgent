package com.openagent.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
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
        // innerPadding consumed by screens; placeholder avoids lint warnings.
        NavHost(
            navController = navController,
            startDestination = "devices",
            modifier = androidx.compose.ui.Modifier.padding(innerPadding)
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
