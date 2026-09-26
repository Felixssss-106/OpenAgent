package com.openagent.android.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.RoundRect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlin.math.cos
import kotlin.math.sin

/**
 * The glyphs the Android artboards draw, painted directly: material-icons-extended
 * would ship its whole set into an unminified release, and the handful core
 * exposes are not the shapes the design uses. Geometry is authored on a 24-unit
 * grid and multiplied to pixels per point — a canvas transform kept putting the
 * top of every glyph outside the view.
 */
enum class Glyph { Sparkle, Tasks, Devices, Settings, ArrowUp, ChevronDown, ChevronLeft, ChevronRight, Bolt, Info }

private const val WEIGHT = 1.9f

@Composable
fun GlyphIcon(
    glyph: Glyph,
    tint: Color,
    modifier: Modifier = Modifier,
    size: Dp = 20.dp,
) {
    Canvas(modifier = modifier.size(size)) {
        val s = size.toPx() / 24f
        val stroke = Stroke(width = WEIGHT * s, cap = StrokeCap.Round)

        when (glyph) {
            Glyph.Sparkle -> {
                star(11.4f, 13.6f, 8.2f, s, tint)
                star(18.6f, 5.4f, 4.4f, s, tint)
                star(5.4f, 5.0f, 2.9f, s, tint)
            }

            Glyph.Tasks -> drawPath(grid(s) {
                moveTo(2.6f, 6.4f).lineTo(4.5f, 8.3f).lineTo(8.6f, 4.2f)
                moveTo(2.6f, 14.4f).lineTo(4.5f, 16.3f).lineTo(8.6f, 12.2f)
                moveTo(12f, 6.2f).lineTo(21.4f, 6.2f)
                moveTo(12f, 14.3f).lineTo(21.4f, 14.3f)
                moveTo(12f, 20.6f).lineTo(18.6f, 20.6f)
            }, tint, style = stroke)

            Glyph.Devices -> drawPath(grid(s) {
                roundRect(2.2f, 4.2f, 21.8f, 16.2f, 2.4f)
                moveTo(12f, 16.2f).lineTo(12f, 19.6f)
                moveTo(7.6f, 20f).lineTo(16.4f, 20f)
            }, tint, style = stroke)

            Glyph.Settings -> {
                val c = Offset(12f * s, 12f * s)
                drawCircle(tint, 4.9f * s, c, style = Stroke(1.9f * s))
                drawCircle(tint, 2.0f * s, c, style = Stroke(1.6f * s))
                repeat(8) { index ->
                    val angle = Math.toRadians(index * 45.0)
                    val dx = cos(angle).toFloat()
                    val dy = sin(angle).toFloat()
                    drawLine(
                        tint,
                        c + Offset(dx * 5.9f * s, dy * 5.9f * s),
                        c + Offset(dx * 8.2f * s, dy * 8.2f * s),
                        2.1f * s,
                        StrokeCap.Round,
                    )
                }
            }

            Glyph.ArrowUp -> drawPath(grid(s) {
                moveTo(12f, 19f).lineTo(12f, 5.6f)
                moveTo(7f, 10.6f).lineTo(12f, 5.6f).lineTo(17f, 10.6f)
            }, tint, style = stroke)

            Glyph.ChevronDown -> drawPath(grid(s) {
                moveTo(6.8f, 9.8f).lineTo(12f, 15f).lineTo(17.2f, 9.8f)
            }, tint, style = stroke)

            Glyph.ChevronLeft -> drawPath(grid(s) {
                moveTo(14.2f, 6.4f).lineTo(8.6f, 12f).lineTo(14.2f, 17.6f)
            }, tint, style = stroke)

            Glyph.ChevronRight -> drawPath(grid(s) {
                moveTo(9.8f, 6.4f).lineTo(15.4f, 12f).lineTo(9.8f, 17.6f)
            }, tint, style = stroke)

            Glyph.Bolt -> drawPath(
                grid(s) {
                    moveTo(13.7f, 2.6f).lineTo(5.3f, 13.7f).lineTo(11f, 13.7f)
                    lineTo(10.3f, 21.4f).lineTo(18.7f, 10.3f).lineTo(13f, 10.3f).closePath()
                },
                tint,
            )

            Glyph.Info -> {
                drawCircle(tint, 9.4f * s, Offset(12f * s, 12f * s), style = Stroke(1.6f * s))
                drawPath(grid(s) { moveTo(12f, 11.4f).lineTo(12f, 16.8f) }, tint, style = stroke)
                drawCircle(tint, 1.15f * s, Offset(12f * s, 7.6f * s))
            }
        }
    }
}

/** Builds a path straight in pixels, so nothing has to be transformed afterwards. */
private class Grid(private val s: Float) {
    val path = Path()

    fun moveTo(x: Float, y: Float) = apply { path.moveTo(x * s, y * s) }

    fun lineTo(x: Float, y: Float) = apply { path.lineTo(x * s, y * s) }

    fun roundRect(left: Float, top: Float, right: Float, bottom: Float, radius: Float) = apply {
        path.addRoundRect(
            RoundRect(left * s, top * s, right * s, bottom * s, CornerRadius(radius * s)),
        )
    }

    fun closePath() = apply { path.close() }
}

private fun grid(s: Float, builder: Grid.() -> Unit): Path = Grid(s).apply(builder).path

/** The brand star: four outer tips with four pinched waists. */
private fun DrawScope.star(cx: Float, cy: Float, radius: Float, s: Float, tint: Color) {
    val inner = radius * 0.30f
    val path = Path()
    repeat(4) { index ->
        val outer = Math.toRadians(index * 90.0 - 90.0)
        val between = Math.toRadians(index * 90.0 - 45.0)
        val ox = ((cx + cos(outer) * radius) * s).toFloat()
        val oy = ((cy + sin(outer) * radius) * s).toFloat()
        if (index == 0) path.moveTo(ox, oy) else path.lineTo(ox, oy)
        path.lineTo(
            ((cx + cos(between) * inner) * s).toFloat(),
            ((cy + sin(between) * inner) * s).toFloat(),
        )
    }
    path.close()
    drawPath(path, tint)
}
