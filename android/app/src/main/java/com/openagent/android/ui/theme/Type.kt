package com.openagent.android.ui.theme

import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.em
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * The type scale from design/tokens.css section 3, spelled the way the artboards
 * use it. Named by role rather than mapped onto Material's headline/body titles,
 * because Material's roles do not line up with the design's (its "bodyLarge" is
 * 16sp where the design's body is 15px).
 */
object Type {
    val hero = TextStyle(
        fontSize = 44.sp,
        lineHeight = 48.sp,
        fontWeight = FontWeight.Bold,
        letterSpacing = (-0.022).em,
    )

    val title = TextStyle(fontSize = 24.sp, lineHeight = 30.sp, fontWeight = FontWeight.SemiBold)

    val heading = TextStyle(
        fontSize = 17.sp,
        lineHeight = 24.sp,
        fontWeight = FontWeight.SemiBold,
        letterSpacing = (-0.01).em,
    )

    /** Chat and answer copy: the design's 17px heading size at body weight. */
    val prose = TextStyle(fontSize = 17.sp, lineHeight = 26.sp, fontWeight = FontWeight.Normal)

    val body = TextStyle(fontSize = 15.sp, lineHeight = 22.sp)

    val callout = TextStyle(fontSize = 14.sp, lineHeight = 20.sp)

    val caption = TextStyle(fontSize = 13.sp, lineHeight = 18.sp)

    val micro = TextStyle(
        fontSize = 11.sp,
        lineHeight = 16.sp,
        fontWeight = FontWeight.SemiBold,
        letterSpacing = 0.062.em,
    )
}

/** Corner radii from the design: content containers 28, nested surfaces inside them
 *  tighter (内圆角 ≤ 外圆角), and the tab bar and composer are capsules, not radii. */
object Shape {
    val card = 28.dp
    val inner = 12.dp
    /** Content inset the design states for Android (manifest: 水平外边距 20). */
    val gutter = 20.dp
    /** The floating composer and tab bar sit a little wider than content. */
    val barInset = 16.dp
    val tabHeight = 60.dp
    val composerHeight = 52.dp
    /**
     * A list row inside a card. Artboards 26/27 put a card's two rows 77dp apart
     * border to border, so a row plus its separator is 38.5; 38 + the 1dp
     * RowDivider lands on it. The 46 this used to carry measured 47.
     */
    val rowHeight = 38.dp
    val barGap = 4.dp
    /** --shadow-float: the halo under the two floating capsules reaches ~12dp out. */
    val floatElevation = 5.5.dp
    val navbarHeight = 44.dp
    val backButton = 36.dp
}
