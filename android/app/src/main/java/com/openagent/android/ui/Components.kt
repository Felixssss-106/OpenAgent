package com.openagent.android.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.openagent.android.ui.theme.LocalPalette
import com.openagent.android.ui.theme.Palette
import com.openagent.android.ui.theme.Shape
import com.openagent.android.ui.theme.Type

/** Shorthand for the palette the current screen is drawing with. */
val ink: Palette @Composable get() = LocalPalette.current

@Composable
fun OaCard(
    modifier: Modifier = Modifier,
    surface: Color = ink.bgSurface,
    border: Color = Color.Transparent,
    radius: Int = 20,
    content: @Composable ColumnScope.() -> Unit,
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(radius.dp))
            .background(surface)
            .then(if (border == Color.Transparent) Modifier else Modifier.border(1.dp, border))
            .padding(horizontal = 20.dp, vertical = 4.dp),
        content = content,
    )
}

@Composable
fun RowDivider(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(1.dp)
            .background(ink.borderSubtle),
    )
}

/** The 44px bar every secondary artboard draws: centred title, circular back. */
@Composable
fun OaNavBar(title: String, onBack: () -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(Shape.navbarHeight),
    ) {
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .padding(start = Shape.gutter - 10.dp)
                .size(Shape.backButton)
                .clip(CircleShape)
                .background(ink.bgCanvas)
                .border(1.dp, ink.borderSubtle, CircleShape)
                .clickable(onClick = onBack),
            contentAlignment = Alignment.Center,
        ) {
            GlyphIcon(Glyph.ChevronLeft, tint = ink.accent, size = 18.dp)
        }

        Text(
            text = title,
            style = Type.heading,
            color = ink.textPrimary,
            textAlign = TextAlign.Center,
            modifier = Modifier.align(Alignment.Center),
        )
    }
    RowDivider()
}

@Composable
fun SectionLabel(text: String, modifier: Modifier = Modifier) {
    Text(
        text = text,
        style = Type.caption,
        color = ink.textQuaternary,
        modifier = modifier.padding(start = 2.dp, bottom = 8.dp),
    )
}

@Composable
fun ValueRow(
    label: String,
    value: String,
    valueColor: Color = ink.textTertiary,
    onClick: (() -> Unit)? = null,
    showChevron: Boolean = true,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(46.dp)
            .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(label, style = Type.body, color = ink.textPrimary)
        Spacer(Modifier.weight(1f))
        Text(value, style = Type.caption, color = valueColor)
        if (showChevron) {
            Spacer(Modifier.width(6.dp))
            GlyphIcon(Glyph.ChevronRight, tint = ink.textQuaternary, size = 14.dp)
        }
    }
}

@Composable
fun StatusDot(color: Color, size: Int = 8) {
    Box(
        modifier = Modifier
            .size(size.dp)
            .clip(CircleShape)
            .background(color),
    )
}

/** The floating capsule tab bar every Android artboard draws. */
@Composable
fun OaTabBar(selected: String, onSelect: (String) -> Unit) {
    val tabs = listOf(
        "agent" to ("Agent" to Glyph.Sparkle),
        "tasks" to ("任务" to Glyph.Tasks),
        "devices" to ("设备" to Glyph.Devices),
        "settings" to ("设置" to Glyph.Settings),
    )

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 14.dp)
            .height(Shape.tabHeight)
            .clip(RoundedCornerShape(percent = 50))
            .background(ink.bgSurface)
            .padding(4.dp),
        horizontalArrangement = Arrangement.spacedBy(2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        tabs.forEach { (route, pair) ->
            val (label, icon) = pair
            val active = route == selected
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight()
                    .clip(RoundedCornerShape(18.dp))
                    .background(if (active) ink.tabHighlight else Color.Transparent)
                    .clickable { onSelect(route) },
            ) {
                GlyphIcon(
                    glyph = icon,
                    tint = if (active) ink.accent else ink.iconMuted,
                    size = 22.dp,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    text = label,
                    style = Type.caption,
                    fontSize = 11.sp,
                    color = if (active) ink.accent else ink.textTertiary,
                )
            }
        }
    }
}
