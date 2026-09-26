package com.openagent.android.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.openagent.android.data.model.Device
import com.openagent.android.ui.OaCard
import com.openagent.android.ui.OaNavBar
import com.openagent.android.ui.RowDivider
import com.openagent.android.ui.ink
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.Type
import com.openagent.android.viewmodel.MainViewModel

/** Artboard 26: one card per beacon, name over a quiet platform line. */
@Composable
fun DevicesScreen(onBack: () -> Unit, onPick: (Device) -> Unit, vm: MainViewModel = viewModel()) {
    val devices by vm.devices.collectAsStateWithLifecycle()

    Column(modifier = Modifier.fillMaxSize()) {
        OaNavBar("设备", onBack)

        Text(
            text = "${devices.size} 台已配对",
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
                        modifier = Modifier.padding(top = 4.dp, bottom = 16.dp),
                    )
                }
            }
        }
    }
}
