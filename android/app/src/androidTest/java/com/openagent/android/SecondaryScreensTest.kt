package com.openagent.android

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.platform.app.InstrumentationRegistry
import com.openagent.android.data.model.Device
import com.openagent.android.ui.OaTabBar
import com.openagent.android.ui.screens.CommandTask
import com.openagent.android.ui.screens.DevicesScreenContent
import com.openagent.android.ui.screens.TasksScreenContent
import com.openagent.android.ui.theme.LocalPalette
import com.openagent.android.ui.theme.OpenAgentTheme
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

/**
 * Artboards 25/26 draw a task card and device cards, but the emulator has no
 * paired host and no sent command, so the app can only ever show the empty
 * states here. These render the same composables the routes do, with sample
 * rows, so the card layout is actually looked at instead of assumed.
 */
@RunWith(JUnit4::class)
class SecondaryScreensTest {

    @get:Rule
    val rule = createComposeRule()

    @Test
    fun taskRowsMatchArtboard25() {
        val sent = System.currentTimeMillis() - 120_000L
        rule.setContent {
            OpenAgentTheme {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(LocalPalette.current.bgCanvas),
                ) {
                    TasksScreenContent(
                        tasks = listOf(
                            CommandTask("删除重复文件", sent, replied = false),
                            CommandTask("查看系统信息", sent, replied = true),
                        ),
                        onBack = {},
                    )
                    OaTabBar(selected = "tasks") {}
                }
            }
        }

        rule.waitForIdle()
        rule.onNodeWithText("1 等待回复 · 1 已回复").assertIsDisplayed()
        rule.onNodeWithText("删除重复文件").assertIsDisplayed()
        rule.onNodeWithText("等待回复").assertIsDisplayed()
        rule.onNodeWithText("已回复").assertIsDisplayed()
        hold()
    }

    @Test
    fun deviceCardsMatchArtboard26() {
        rule.setContent {
            OpenAgentTheme {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(LocalPalette.current.bgCanvas),
                ) {
                    DevicesScreenContent(
                        devices = listOf(
                            Device("host-1", "DESKTOP-XXXX", "Windows", "11", 0L),
                            Device("phone-2", "Pixel 9", "Android", "15", 0L),
                        ),
                        onBack = {},
                        onPick = {},
                    )
                    OaTabBar(selected = "devices") {}
                }
            }
        }

        rule.waitForIdle()
        rule.onNodeWithText("2 台已配对").assertIsDisplayed()
        rule.onNodeWithText("DESKTOP-XXXX").assertIsDisplayed()
        rule.onNodeWithText("Windows 11 · 直连").assertIsDisplayed()
        rule.onNodeWithText("Pixel 9").assertIsDisplayed()
        hold()
    }

    private fun hold() {
        InstrumentationRegistry.getArguments().getString("hold")?.toIntOrNull()?.let { seconds ->
            Thread.sleep(seconds * 1000L)
        }
    }
}
