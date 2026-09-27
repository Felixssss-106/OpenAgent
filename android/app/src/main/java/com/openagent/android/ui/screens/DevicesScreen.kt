package com.openagent.android.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.openagent.android.data.PairingManager
import com.openagent.android.data.model.Device
import com.openagent.android.ui.OaCard
import com.openagent.android.ui.OaNavBar
import com.openagent.android.ui.RowDivider
import com.openagent.android.ui.ink
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.Type
import com.openagent.android.viewmodel.MainViewModel

/**
 * Artboard 26: one card per beacon, name over a quiet platform line. v2 adds
 * the one thing the artboards could not draw: a 配对 action, because a beacon
 * is neither trust nor pairing (docs/protocol.md §4).
 */
@Composable
fun DevicesScreen(onBack: () -> Unit, onPick: (Device) -> Unit, vm: MainViewModel = viewModel()) {
    val devices by vm.devices.collectAsStateWithLifecycle()
    val pairedIds by vm.pairedIds.collectAsStateWithLifecycle()
    val challenge by vm.pairingChallenge.collectAsStateWithLifecycle()
    DevicesScreenContent(
        devices = devices,
        onBack = onBack,
        onPick = onPick,
        pairedIds = pairedIds,
        challenge = challenge,
        onPair = vm::startPairing,
        onPin = vm::submitPin,
        onCancelPairing = vm::cancelPairing,
    )
}

/** The same screen without the view model, so the device cards can be screenshotted. */
@Composable
internal fun DevicesScreenContent(
    devices: List<Device>,
    onBack: () -> Unit,
    onPick: (Device) -> Unit,
    pairedIds: Set<String> = emptySet(),
    challenge: PairingManager.Challenge? = null,
    onPair: (Device) -> Unit = {},
    onPin: (String) -> Unit = {},
    onCancelPairing: () -> Unit = {},
) {
    Column(modifier = Modifier.fillMaxSize()) {
        OaNavBar("设备", onBack)

        Text(
            // 发现，不是配对：a beacon is neither trust nor pairing — the honest
            // word for what this list actually holds.
            text = "${devices.size} 台已发现",
            style = Type.caption,
            color = ink.textTertiary,
            modifier = Modifier.padding(start = Shape.gutter, top = 14.dp, bottom = 12.dp),
        )
        RowDivider()

        if (devices.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(
                    "同一局域网内还没有发现 Windows 主机\n打开电脑端的 OpenAgent 后会自动出现",
                    style = Type.body,
                    color = ink.textTertiary,
                    textAlign = TextAlign.Center,
                )
            }
            return@Column
        }

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = Shape.gutter, vertical = 16.dp),
        ) {
            devices.forEachIndexed { index, device ->
                val paired = device.id in pairedIds
                OaCard(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = if (index == devices.lastIndex) 0.dp else 14.dp)
                        .clickable { onPick(device) },
                ) {
                    Text(
                        device.name,
                        style = Type.heading,
                        color = ink.textPrimary,
                        modifier = Modifier.padding(top = 16.dp),
                    )
                    Text(
                        "${device.platform} ${device.version} · 直连",
                        style = Type.caption,
                        color = ink.textTertiary,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                    Text(
                        text = if (paired) "已配对" else "配对",
                        style = Type.caption,
                        color = if (paired) ink.textTertiary else ink.accent,
                        modifier = Modifier
                            .padding(top = 4.dp, bottom = 16.dp)
                            .clickable(enabled = !paired) { onPair(device) },
                    )
                }
            }
        }
    }

    challenge?.let { active ->
        PairingPinDialog(
            challenge = active,
            onConfirm = onPin,
            onDismiss = onCancelPairing,
        )
    }
}

@Composable
private fun PairingPinDialog(
    challenge: PairingManager.Challenge,
    onConfirm: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    var code by remember(challenge) { mutableStateOf("") }
    var mismatch by remember(challenge) { mutableStateOf(false) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("输入配对码") },
        text = {
            Column {
                Text(
                    "在电脑端的弹窗查看配对码，在这里输入同一组数字。",
                    style = Type.caption,
                    color = ink.textTertiary,
                )
                Spacer(Modifier.height(12.dp))
                TextField(
                    value = code,
                    onValueChange = { value ->
                        mismatch = false
                        code = value.filter(Char::isDigit).take(6)
                    },
                    singleLine = true,
                    placeholder = { Text("6 位数字") },
                    isError = mismatch,
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = ink.bgSurface,
                        unfocusedContainerColor = ink.bgSurface,
                    ),
                    modifier = Modifier.fillMaxWidth(),
                )
                if (mismatch) {
                    Spacer(Modifier.height(6.dp))
                    Text("配对码不符，请对照电脑端重新输入", style = Type.caption, color = ink.statusError)
                }
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    if (code == challenge.expectedPin) {
                        onConfirm(code)
                    } else {
                        mismatch = true
                    }
                },
                enabled = code.length == 6,
            ) { Text("配对") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("取消") } },
    )
}
