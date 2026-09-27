package com.openagent.android.ui.theme

import androidx.compose.runtime.Immutable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

/**
 * design/tokens.css, palette graphite. The CSS file is the source of truth;
 * these two instances mirror its light and dark sections so the phone and the
 * desktop ship the same colours rather than Material You's wallpaper-derived
 * palette, which matched nothing in the artboards.
 */
@Immutable
data class Palette(
    val bgCanvas: Color,
    val bgSurface: Color,
    val bgSunken: Color,
    val bgInset: Color,
    val borderSubtle: Color,
    val borderDefault: Color,
    val textPrimary: Color,
    val textSecondary: Color,
    val textTertiary: Color,
    val textQuaternary: Color,
    val accent: Color,
    val accentQuiet: Color,
    val onAccent: Color,
    val tabHighlight: Color,
    val iconMuted: Color,
    val statusOnline: Color,
    val statusError: Color,
    val statusPending: Color,
    /**
     * Peak coverage of --shadow-float where it lands on this theme's canvas.
     * Measured off artboards 07/08 one pixel below the floating capsule: light
     * reads 20/255 on white (0.078), dark reads 10 to 6 (0.40). The two are not
     * a factor apart by accident — a black shadow on a canvas of 10 has to be
     * far stronger to move the pixel at all.
     */
    val shadowAlpha: Float,
)

val LightPalette = Palette(
    bgCanvas = Color(0xFFFFFFFF),
    bgSurface = Color(0xFFF7F7FA),
    bgSunken = Color(0xFFF2F2F7),
    bgInset = Color(0xFFE5E5EA),
    borderSubtle = Color(0xFFE5E5EA),
    borderDefault = Color(0xFFD1D1D6),
    textPrimary = Color(0xFF1D1D1F),
    textSecondary = Color(0xFF6E6E73),
    textTertiary = Color(0xFF8E8E93),
    textQuaternary = Color(0xFFAEAEB2),
    accent = Color(0xFF007AFF),
    accentQuiet = Color(0xFFE8F2FF),
    onAccent = Color(0xFFFFFFFF),
    tabHighlight = Color(0xFFFFFFFF),
    iconMuted = Color(0xFF8E8E93),
    statusOnline = Color(0xFF34C759),
    statusError = Color(0xFFFF3B30),
    statusPending = Color(0xFFFF9500),
    shadowAlpha = 0.42f,
)

val DarkPalette = Palette(
    bgCanvas = Color(0xFF0A0A0A),
    bgSurface = Color(0xFF1C1C1E),
    bgSunken = Color(0xFF000000),
    bgInset = Color(0xFF2C2C2E),
    borderSubtle = Color(0xFF2C2C2E),
    borderDefault = Color(0xFF3A3A3C),
    textPrimary = Color(0xFFF5F5F7),
    textSecondary = Color(0xFFAEAEB2),
    textTertiary = Color(0xFF8E8E93),
    textQuaternary = Color(0xFF6E6E73),
    accent = Color(0xFF2E8DFF),
    accentQuiet = Color(0xFF003B82),
    onAccent = Color(0xFF000000),
    tabHighlight = Color(0xFF3A3A3C),
    iconMuted = Color(0xFF8E8E93),
    statusOnline = Color(0xFF30D158),
    statusError = Color(0xFFFF453A),
    statusPending = Color(0xFFFF9F0A),
    shadowAlpha = 1.00f,
)

val LocalPalette = staticCompositionLocalOf { LightPalette }
