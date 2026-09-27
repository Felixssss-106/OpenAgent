package com.openagent.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.openagent.android.ui.OaTabBar
import com.openagent.android.ui.screens.AgentScreen
import com.openagent.android.ui.screens.DevicesScreen
import com.openagent.android.ui.screens.SettingsScreen
import com.openagent.android.ui.screens.TasksScreen
import com.openagent.android.ui.theme.OpenAgentTheme
import com.openagent.android.ui.theme.ThemePrefs
import com.openagent.android.viewmodel.MainViewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        ThemePrefs.load(applicationContext)
        enableEdgeToEdge()
        setContent {
            OpenAgentTheme {
                OpenAgentApp()
            }
        }
    }
}

/**
 * Four tabs under a floating capsule bar, Agent first — where every Android
 * artboard starts. The bar stays put across tabs because 07-12 and 25-30 all
 * draw it.
 */
@Composable
fun OpenAgentApp() {
    val navController = rememberNavController()
    val backStack by navController.currentBackStackEntryAsState()
    val route = backStack?.destination?.route ?: "agent"
    // One activity-scoped view model: created here, outside the NavHost, so a
    // device picked on 设备 is visible to the Agent page as the send target.
    val vm: MainViewModel = viewModel()

    Scaffold(
        contentWindowInsets = WindowInsets.statusBars,
        bottomBar = {
            Box(modifier = Modifier.windowInsetsPadding(WindowInsets.navigationBars)) {
                OaTabBar(selected = route.substringBefore('/')) { target ->
                    if (target != route) {
                        navController.navigate(target) {
                            popUpTo("agent") { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    }
                }
            }
        },
    ) { innerPadding ->
        NavHost(
            navController = navController,
            startDestination = "agent",
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(bottom = 8.dp),
        ) {
            composable("agent") { AgentScreen(vm = vm) }
            composable("tasks") { TasksScreen(onBack = { navController.navigate("agent") }, vm = vm) }
            composable("devices") {
                DevicesScreen(
                    onBack = { navController.navigate("agent") },
                    onPick = { device ->
                        vm.selectTarget(device)
                        navController.navigate("agent")
                    },
                    vm = vm,
                )
            }
            composable("settings") { SettingsScreen(onBack = { navController.navigate("agent") }) }
        }
    }
}
