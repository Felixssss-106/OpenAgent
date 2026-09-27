package com.openagent.android.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.ui.Glyph
import com.openagent.android.ui.GlyphIcon
import com.openagent.android.ui.StatusDot
import com.openagent.android.ui.ink
import com.openagent.android.ui.oaFloat
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.Type
import com.openagent.android.viewmodel.MainViewModel
import java.util.Calendar
import java.util.Locale

/**
 * The agent surface: 起始页 until the first command, then 对话态 (artboards
 * 07-10). The phone is a client — it sends a command and shows what the Windows
 * host answers — so there is no tool-call or approval state to draw here: the
 * envelope carries plain text both ways (docs/protocol.md), and the gate that
 * artboard 11 draws lives on the host.
 */
@Composable
fun AgentScreen(vm: MainViewModel = viewModel()) {
    val messages by vm.messages.collectAsStateWithLifecycle()
    val devices by vm.devices.collectAsStateWithLifecycle()
    var draft by remember { mutableStateOf("") }

    val host = devices.firstOrNull { it.platform.equals("windows", ignoreCase = true) }
        ?: devices.firstOrNull()

    AgentScreenContent(
        messages = messages,
        hostName = host?.name,
        canSend = host != null,
        draft = draft,
        onDraft = { draft = it },
        onSend = {
            val text = draft.trim()
            if (text.isNotEmpty() && host != null) {
                vm.sendCommand(host.id, text)
                draft = ""
            }
        },
    )
}

/** The same surface without the view model, so the layout can be rendered to a screenshot. */
@Composable
internal fun AgentScreenContent(
    messages: List<ChatMessage>,
    hostName: String?,
    canSend: Boolean,
    draft: String,
    onDraft: (String) -> Unit,
    onSend: () -> Unit,
) {
    Column(modifier = Modifier.fillMaxSize()) {
        if (messages.isEmpty()) {
            StartState(hostName = hostName, modifier = Modifier.weight(1f))
        } else {
            ChatState(messages = messages, modifier = Modifier.weight(1f))
        }

        if (messages.isNotEmpty()) {
            MetaRow(hostName = hostName)
        }

        Composer(draft = draft, onDraft = onDraft, onSend = onSend, enabled = canSend)
        // Artboard 07 leaves 12dp between the composer's border and the tab bar's.
        // The NavHost already contributes 8 of that to every route, so this is the rest.
        Spacer(Modifier.height(Shape.barGap))
    }
}

@Composable
private fun StartState(hostName: String?, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.Center,
    ) {
        val greeting = greetingLines()
        Text(greeting.first, style = Type.hero, color = ink.textPrimary)
        Text(greeting.second, style = Type.hero, color = ink.textPrimary)

        Spacer(Modifier.height(48.dp))

        Row(
            modifier = Modifier
                .height(36.dp)
                .clip(RoundedCornerShape(percent = 50))
                // bgSunken, not bgSurface: artboards 07/08 draw the status pill one
                // step below the cards it sits beside (#F2F2F7 / #000000).
                .background(ink.bgSunken)
                .padding(horizontal = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            GlyphIcon(Glyph.Info, tint = ink.textQuaternary, size = 14.dp)
            Spacer(Modifier.width(8.dp))
            Text(
                text = if (hostName != null) "已连接 $hostName · 直连" else "等待 Windows 主机",
                style = Type.caption,
                color = ink.textSecondary,
            )
        }
    }
}

/** The artboards force two lines with "Good" alone on the first. */
private fun greetingLines(): Pair<String, String> {
    val greeting = when (Calendar.getInstance().get(Calendar.HOUR_OF_DAY)) {
        in 5..11 -> "Good morning"
        in 12..17 -> "Good afternoon"
        in 18..21 -> "Good evening"
        else -> "Good night"
    }
    val space = greeting.indexOf(' ')
    return greeting.substring(0, space) to greeting.substring(space + 1)
}

@Composable
private fun ChatState(messages: List<ChatMessage>, modifier: Modifier = Modifier) {
    val listState = rememberLazyListState()
    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) listState.animateScrollToItem(messages.lastIndex)
    }

    LazyColumn(
        state = listState,
        modifier = modifier.fillMaxWidth(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(
            start = Shape.gutter, end = Shape.gutter, top = 20.dp, bottom = 12.dp,
        ),
        verticalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        items(messages, key = { it.id }) { message ->
            // A lazy item is one node, so each turn needs its own column.
            Column(modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp)) {
                when (message.direction) {
                    ChatMessage.Direction.OUT -> {
                        Text("你", style = Type.caption, color = ink.textQuaternary)
                        Text(message.text, style = Type.heading, color = ink.textPrimary)
                    }

                    ChatMessage.Direction.IN -> {
                        Text(
                            text = "AGENT · OPENAGENT",
                            style = Type.micro,
                            color = ink.textQuaternary,
                        )
                        Spacer(Modifier.height(6.dp))
                        Text(message.text, style = Type.prose, color = ink.textPrimary)
                    }
                }
            }
        }
    }
}

@Composable
private fun MetaRow(hostName: String?) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(20.dp)
            .padding(horizontal = Shape.gutter),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        StatusDot(ink.statusOnline)
        Spacer(Modifier.width(8.dp))
        Text(
            text = "${hostName ?: "未连接"} · 直连",
            style = Type.caption,
            color = ink.textTertiary,
        )
    }
    Spacer(Modifier.height(14.dp))
}

@Composable
private fun Composer(draft: String, onDraft: (String) -> Unit, onSend: () -> Unit, enabled: Boolean) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Shape.barInset)
            .height(Shape.composerHeight)
            .oaFloat(RoundedCornerShape(percent = 50))
            .clip(RoundedCornerShape(percent = 50))
            .background(ink.bgSurface)
            .border(1.dp, ink.borderDefault, RoundedCornerShape(percent = 50))
            .padding(start = 20.dp, end = 8.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            androidx.compose.foundation.text.BasicTextField(
                value = draft,
                onValueChange = onDraft,
                textStyle = LocalTextStyle.current.merge(Type.body).copy(color = ink.textPrimary),
                singleLine = true,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Send),
                keyboardActions = KeyboardActions(onSend = { onSend() }),
                modifier = Modifier.weight(1f),
                decorationBox = { field ->
                    Box(contentAlignment = Alignment.CenterStart) {
                        if (draft.isEmpty()) {
                            Text("说点什么…", style = Type.body, color = ink.textQuaternary)
                        }
                        field()
                    }
                },
            )

            Box(
                modifier = Modifier
                    .size(32.dp)
                    .clip(CircleShape)
                    // Artboards 07/08 draw the send disc filled while the field is
                    // still empty, so the fill is not the enabled signal here; only
                    // the click is gated.
                    .background(ink.accent)
                    .clickable(enabled = enabled, onClick = onSend),
                contentAlignment = Alignment.Center,
            ) {
                GlyphIcon(Glyph.ArrowUp, tint = ink.onAccent, size = 17.dp)
            }
        }
    }
}
