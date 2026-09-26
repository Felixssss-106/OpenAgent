package com.openagent.android.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalView
import androidx.core.content.edit
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsControllerCompat

/** The 主题 row on the settings screen; persisted so the phone remembers it. */
enum class ThemeMode(val label: String) {
    System("跟随系统"),
    Light("浅色"),
    Dark("深色"),
    ;

    companion object {
        fun of(name: String?): ThemeMode = entries.firstOrNull { it.name == name } ?: System
    }
}

/**
 * Small enough to justify SharedPreferences over DataStore: one string, read at
 * startup and written when the user picks another palette.
 */
object ThemePrefs {
    private const val KEY = "ui.theme"

    var mode: ThemeMode by mutableStateOf(ThemeMode.System)
        private set

    fun load(context: android.content.Context) {
        val stored = context.getSharedPreferences("openagent", android.content.Context.MODE_PRIVATE)
            .getString(KEY, null)
        mode = ThemeMode.of(stored)
    }

    fun save(context: android.content.Context, chosen: ThemeMode) {
        context.getSharedPreferences("openagent", android.content.Context.MODE_PRIVATE)
            .edit { putString(KEY, chosen.name) }
        mode = chosen
    }
}

@Composable
fun OpenAgentTheme(content: @Composable () -> Unit) {
    val dark = when (ThemePrefs.mode) {
        ThemeMode.Light -> false
        ThemeMode.Dark -> true
        ThemeMode.System -> isSystemInDarkTheme()
    }
    val palette = if (dark) DarkPalette else LightPalette

    // The status bar icons have to flip with the palette, or a light canvas
    // leaves white-on-white system glyphs behind.
    val view = LocalView.current
    SideEffect {
        val window = (view.context as? android.app.Activity)?.window ?: return@SideEffect
        WindowCompat.setDecorFitsSystemWindows(window, false)
        WindowInsetsControllerCompat(window, view).isAppearanceLightStatusBars = !dark
    }

    CompositionLocalProvider(LocalPalette provides palette) {
        MaterialTheme(colorScheme = palette.toScheme(dark), content = content)
    }
}

/**
 * Material's own widgets (text cursor, slider track) read the colour scheme, so
 * the design palette has to be projected onto it even though every screen here
 * draws from [LocalPalette] directly.
 */
private fun Palette.toScheme(dark: Boolean): ColorScheme {
    val base = if (dark) darkColorScheme() else lightColorScheme()
    return base.copy(
        primary = accent,
        onPrimary = onAccent,
        primaryContainer = accentQuiet,
        onPrimaryContainer = textPrimary,
        secondary = accent,
        onSecondary = onAccent,
        background = bgCanvas,
        onBackground = textPrimary,
        surface = bgCanvas,
        onSurface = textPrimary,
        surfaceVariant = bgSurface,
        onSurfaceVariant = textSecondary,
        surfaceContainer = bgSurface,
        surfaceContainerHigh = bgSurface,
        surfaceContainerLow = bgSunken,
        outline = textTertiary,
        outlineVariant = borderSubtle,
        error = statusError,
    )
}
