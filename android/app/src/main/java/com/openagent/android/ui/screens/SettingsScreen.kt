package com.openagent.android.ui.screens

import android.os.Build
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.openagent.android.OpenAgentApplication
import com.openagent.android.ui.OaCard
import com.openagent.android.ui.OaNavBar
import com.openagent.android.ui.RowDivider
import com.openagent.android.ui.SectionLabel
import com.openagent.android.ui.ValueRow
import com.openagent.android.ui.ink
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.ThemeMode
import com.openagent.android.ui.theme.ThemePrefs
import com.openagent.android.ui.theme.Type

/**
 * Artboard 27's grouping and row anatomy, filled with what this build actually
 * knows. The mockup's 开机启动 / 默认 Agent / 权限模式 / Relay rows are left out:
 * the phone has no autostart, picks no provider, does not gate permissions and
 * has no relay — drawing them would be a control that answers to nothing.
 */
@Composable
fun SettingsScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val app = context.applicationContext as OpenAgentApplication
    var pickingTheme by remember { mutableStateOf(false) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState()),
    ) {
        OaNavBar("设置", onBack)

        Column(modifier = Modifier.padding(horizontal = Shape.gutter, vertical = 16.dp)) {
            SectionLabel("通用")
            OaCard {
                ValueRow(
                    label = "主题",
                    value = ThemePrefs.mode.label,
                    onClick = { pickingTheme = true },
                )
                RowDivider()
                ValueRow(label = "设备标识", value = app.lanClient.selfId, showChevron = false)
            }

            SectionLabel("网络", modifier = Modifier.padding(top = 24.dp))
            OaCard {
                ValueRow(label = "连接方式", value = "直连 · 局域网")
                RowDivider()
                ValueRow(label = "端口", value = "47819 UDP")
                RowDivider()
                ValueRow(label = "协议", value = "OpenAgent v1")
            }

            SectionLabel("系统", modifier = Modifier.padding(top = 24.dp))
            OaCard {
                ValueRow(label = "Android", value = "${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})")
                RowDivider()
                ValueRow(label = "机型", value = Build.MODEL)
                RowDivider()
                ValueRow(label = "版本", value = "OpenAgent 1.0.0")
            }
        }
    }

    if (pickingTheme) {
        AlertDialog(
            onDismissRequest = { pickingTheme = false },
            title = { Text("主题", style = Type.heading, color = ink.textPrimary) },
            text = {
                Column {
                    ThemeMode.entries.forEach { mode ->
                        Column(modifier = Modifier.clickable {
                            ThemePrefs.save(context, mode)
                            pickingTheme = false
                        }) {
                            androidx.compose.foundation.layout.Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                                RadioButton(selected = mode == ThemePrefs.mode, onClick = {
                                    ThemePrefs.save(context, mode)
                                    pickingTheme = false
                                })
                                Text(mode.label, style = Type.body, color = ink.textPrimary)
                            }
                        }
                    }
                }
            },
            confirmButton = {},
        )
    }
}
