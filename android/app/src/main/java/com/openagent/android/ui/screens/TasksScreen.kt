package com.openagent.android.ui.screens

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
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.openagent.android.data.model.ChatMessage
import com.openagent.android.ui.OaCard
import com.openagent.android.ui.OaNavBar
import com.openagent.android.ui.RowDivider
import com.openagent.android.ui.StatusDot
import com.openagent.android.ui.ink
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.Type
import com.openagent.android.viewmodel.MainViewModel

/**
 * Artboard 25 draws the host's task list; the phone has no task store, so this
 * shows what really is the phone's own record — the commands it sent and whether
 * the host answered. A command with no reply yet is 等待回复, not 运行中.
 */
@Composable
fun TasksScreen(onBack: () -> Unit, vm: MainViewModel = viewModel()) {
    val messages by vm.messages.collectAsStateWithLifecycle()
    TasksScreenContent(rememberCommands(messages), onBack)
}

/** The same screen without the view model, so the card layout can be screenshotted. */
@Composable
internal fun TasksScreenContent(tasks: List<CommandTask>, onBack: () -> Unit) {
    Column(modifier = Modifier.fillMaxSize()) {
        OaNavBar("任务", onBack)

        if (tasks.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(
                    "还没有发出过指令\n在 Agent 页输入指令后会出现在这里",
                    style = Type.body,
                    color = ink.textTertiary,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                )
            }
            return@Column
        }

        Text(
            text = "${tasks.count { !it.replied }} 等待回复 · ${tasks.count { it.replied }} 已回复",
            style = Type.caption,
            color = ink.textTertiary,
            modifier = Modifier.padding(start = Shape.gutter, top = 14.dp, bottom = 12.dp),
        )
        RowDivider()

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = Shape.gutter, vertical = 16.dp),
        ) {
            OaCard {
                tasks.forEachIndexed { index, task ->
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(48.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        StatusDot(if (task.replied) ink.statusOnline else ink.statusPending)
                        Spacer(Modifier.width(10.dp))
                        Text(
                            text = task.text,
                            style = Type.body,
                            color = ink.textPrimary,
                            maxLines = 1,
                            modifier = Modifier.weight(1f),
                        )
                        Spacer(Modifier.width(10.dp))
                        Text(
                            text = if (task.replied) task.replyLabel else "等待回复",
                            style = Type.caption,
                            color = if (task.replied) ink.textTertiary else ink.statusPending,
                        )
                    }
                    if (index != tasks.lastIndex) RowDivider()
                }
            }
        }
    }
}

internal data class CommandTask(
    val text: String,
    val at: Long,
    val replied: Boolean,
    val replyLabel: String = if (replied) "已回复" else "",
)

@Composable
private fun rememberCommands(messages: List<ChatMessage>): List<CommandTask> {
    val sorted = messages.sortedBy { it.ts }
    return remember(messages) {
        sorted.mapNotNull { message ->
            if (message.direction != ChatMessage.Direction.OUT) return@mapNotNull null
            // The host's result envelope echoes the command's id (docs/protocol.md
            // §3), so "已回复" means THIS command was answered — not merely that
            // some later message arrived, which any unrelated inbound traffic
            // used to satisfy.
            val replied = sorted.any {
                it.direction == ChatMessage.Direction.IN && it.id == message.id
            }
            CommandTask(text = message.text, at = message.ts, replied = replied)
        }.reversed()
    }
}
