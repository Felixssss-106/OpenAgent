package com.openagent.android

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.systemBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.assertIsDisplayed
import androidx.test.platform.app.InstrumentationRegistry
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.ui.OaTabBar
import com.openagent.android.ui.screens.AgentScreenContent
import com.openagent.android.ui.theme.LocalPalette
import com.openagent.android.ui.theme.OpenAgentTheme
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

/**
 * Artboards 09/10 (对话态) cannot be reached on an emulator: the phone learns of
 * a host from a LAN broadcast, and the emulator sits behind NAT. This renders the
 * same composable the app does, with sample turns, asserts the artboard's parts
 * are on screen, and — with `-e hold <seconds>` — keeps the frame up so
 * scripts/android-shot-test.sh can screencap it and measure it against the design.
 *
 * The host activity does not go edge-to-edge, so the held frame's bottom 48dp is the
 * emulator's navigation bar and the tab bar sits above it. That is the harness, not the
 * app: the composer and tab bar are measured from the app's own start-page capture
 * (`artifacts/shots/raw-agent-light.png`), which is edge-to-edge. Hiding the bar instead
 * was tried and the emulator answers with its "Viewing full screen" tutorial overlay,
 * which dims the whole frame.
 */
@RunWith(JUnit4::class)
class AgentScreenTest {

    @get:Rule
    val rule = createComposeRule()

    @Test
    fun conversationStateDrawsEveryArtboardPart() {
        rule.setContent {
            OpenAgentTheme {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        // targetSdk 35 forces edge-to-edge; this host is not MainActivity,
                        // so consume the system bars here or the tab bar lands under the
                        // navigation bar.
                        .windowInsetsPadding(WindowInsets.systemBars)
                        .background(LocalPalette.current.bgCanvas),
                ) {
                    // Weighted so the tab bar is not pushed off the bottom: the screen
                    // fills its space, and an unweighted sibling lands below the fold.
                    Box(modifier = Modifier.weight(1f)) {
                        AgentScreenContent(
                            messages = listOf(
                                ChatMessage("u1", ChatMessage.Direction.OUT, "整理一下今天下载的文件。"),
                                ChatMessage(
                                    "a1",
                                    ChatMessage.Direction.IN,
                                    "我发现 23 张图片、4 个 PDF、2 个 ZIP、6 个其他文件，需要移动 35 个文件。",
                                ),
                            ),
                            hostName = "DESKTOP-XXXX",
                            canSend = true,
                            draft = "",
                            onDraft = {},
                            onSend = {},
                        )
                    }
                    OaTabBar(selected = "agent") {}
                }
            }
        }

        rule.waitForIdle()
        rule.onNodeWithText("你").assertIsDisplayed()
        rule.onNodeWithText("整理一下今天下载的文件。").assertIsDisplayed()
        rule.onNodeWithText("AGENT · OPENAGENT").assertIsDisplayed()
        rule.onNodeWithText("我发现 23 张图片、4 个 PDF、2 个 ZIP、6 个其他文件，需要移动 35 个文件。")
            .assertIsDisplayed()
        rule.onNodeWithText("DESKTOP-XXXX · 直连").assertIsDisplayed()
        rule.onNodeWithText("说点什么…").assertIsDisplayed()

        InstrumentationRegistry.getArguments().getString("hold")?.toIntOrNull()?.let { seconds ->
            Thread.sleep(seconds * 1000L)
        }
    }
}
