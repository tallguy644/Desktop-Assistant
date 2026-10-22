package com.tallguy644.syracompanion

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.lerp
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp

// Syra HUD palette: deep navy glass, accent circuits, reactor rings and a red core.
// Accent, rings and core follow the colors chosen in Syra on the PC (or on the phone) and update live.
object Syra {
    val Background = Color(0xFF060D18)
    val Panel = Color(0xFF0A1A28)
    val PanelHigh = Color(0xFF0F2536)
    val Ink = Color(0xFFE6F4FF)
    val Muted = Color(0xFF7FA3B8)
    val Good = Color(0xFF39D98A)
    val Warn = Color(0xFFFFB547)

    /** HUD accent: text, circuits, highlights (Syra's "HUD accent"). */
    var Cyan by mutableStateOf(SyraColors.Original.accentColor)
        private set
    /** Reactor rings (Syra's "reactor rings"). */
    var Rings by mutableStateOf(SyraColors.Original.ringsColor)
        private set
    /** Darker secondary tone derived from accent and rings, for borders and bars. */
    var Teal by mutableStateOf(SyraColors.Original.tealColor)
        private set
    /** Reactor core and alerts (Syra's "reactor core"). */
    var Core by mutableStateOf(SyraColors.Original.coreColor)
        private set

    var colors by mutableStateOf(SyraColors.Original)
        private set

    fun apply(c: SyraColors) {
        colors = c; Cyan = c.accentColor; Rings = c.ringsColor; Teal = c.tealColor; Core = c.coreColor
    }
}

/** The three colors Syra lets you choose, as #RRGGBB strings (same format as Syra's appearance.json). */
data class SyraColors(val accent: String, val rings: String, val core: String) {
    val accentColor get() = parse(accent)
    val ringsColor get() = parse(rings)
    val coreColor get() = parse(core)
    val tealColor get() = lerp(accentColor, ringsColor, 0.3f).let { Color(it.red * 0.62f, it.green * 0.62f, it.blue * 0.62f) }

    companion object {
        val Original = SyraColors("#00E6EB", "#A5D7FF", "#F22330")
        /** Same presets as Syra's Appearance settings. */
        val Presets = listOf(
            "ORIGINAL" to Original,
            "PURPLE" to SyraColors("#C79BFF", "#BFAAFF", "#FF64AD"),
            "GREEN" to SyraColors("#5AFFA0", "#B9FFD3", "#FFB347"),
            "AMBER" to SyraColors("#FFC25C", "#FFE0A1", "#FF6859"),
        )
        private val hex = Regex("^#?[0-9a-fA-F]{6}$")

        /** Normalizes to "#RRGGBB" or returns null if it isn't a six-digit color. */
        fun clean(value: String): String? {
            val v = value.trim()
            return if (hex.matches(v)) ("#" + v.removePrefix("#")).uppercase() else null
        }

        fun parse(value: String): Color {
            val v = clean(value) ?: return Color.Gray
            return Color(0xFF000000 or v.substring(1).toLong(16))
        }

        fun toHex(c: Color): String = String.format(
            java.util.Locale.US, "#%02X%02X%02X",
            (c.red * 255).toInt().coerceIn(0, 255), (c.green * 255).toInt().coerceIn(0, 255), (c.blue * 255).toInt().coerceIn(0, 255),
        )

        fun of(accent: String?, rings: String?, core: String?): SyraColors? {
            val a = clean(accent ?: return null) ?: return null
            val r = clean(rings ?: return null) ?: return null
            val c = clean(core ?: return null) ?: return null
            return SyraColors(a, r, c)
        }
    }
}

@Composable
fun SyraTheme(content: @Composable () -> Unit) {
    val base = Typography()
    val mono = FontFamily.Monospace
    val scheme = darkColorScheme(
        primary = Syra.Cyan,
        onPrimary = Syra.Background,
        primaryContainer = Syra.Teal,
        onPrimaryContainer = Syra.Ink,
        secondary = Syra.Teal,
        onSecondary = Syra.Ink,
        secondaryContainer = Syra.PanelHigh,
        onSecondaryContainer = Syra.Cyan,
        background = Syra.Background,
        onBackground = Syra.Ink,
        surface = Syra.Background,
        onSurface = Syra.Ink,
        surfaceVariant = Syra.Panel,
        onSurfaceVariant = Syra.Muted,
        surfaceContainer = Syra.Panel,
        surfaceContainerHigh = Syra.PanelHigh,
        surfaceContainerHighest = Syra.PanelHigh,
        outline = Color(0xFF1D5A70),
        outlineVariant = Color(0xFF14394A),
        error = Syra.Core,
    )
    MaterialTheme(
        colorScheme = scheme,
        typography = base.copy(
            labelSmall = base.labelSmall.copy(fontFamily = mono),
            labelMedium = base.labelMedium.copy(fontFamily = mono),
        ),
        content = content,
    )
}

/** Microphone glyph (the core icon set has none). */
val MicIcon: ImageVector by lazy {
    ImageVector.Builder("Mic", 24.dp, 24.dp, 24f, 24f).apply {
        path(fill = SolidColor(Color.White)) {
            moveTo(12f, 14f); curveTo(13.66f, 14f, 15f, 12.66f, 15f, 11f); verticalLineTo(5f)
            curveTo(15f, 3.34f, 13.66f, 2f, 12f, 2f); curveTo(10.34f, 2f, 9f, 3.34f, 9f, 5f); verticalLineTo(11f)
            curveTo(9f, 12.66f, 10.34f, 14f, 12f, 14f); close()
        }
        path(stroke = SolidColor(Color.White), strokeLineWidth = 2f, strokeLineCap = StrokeCap.Round, strokeLineJoin = StrokeJoin.Round) {
            moveTo(18f, 11f); curveTo(18f, 14.31f, 15.31f, 17f, 12f, 17f); curveTo(8.69f, 17f, 6f, 14.31f, 6f, 11f)
            moveTo(12f, 17f); verticalLineTo(21f)
        }
    }.build()
}

/** Reactor glyph used for the dashboard tab. */
val ReactorIcon: ImageVector by lazy {
    ImageVector.Builder("Reactor", 24.dp, 24.dp, 24f, 24f).apply {
        path(stroke = SolidColor(Color.White), strokeLineWidth = 2f) {
            moveTo(12f, 3f); arcTo(9f, 9f, 0f, true, true, 11.99f, 3f); close()
        }
        path(fill = SolidColor(Color.White)) {
            moveTo(12f, 8.5f); arcTo(3.5f, 3.5f, 0f, true, true, 11.99f, 8.5f); close()
        }
    }.build()
}

/** Play-in-a-ring glyph used for the media tab. */
val MediaIcon: ImageVector by lazy {
    ImageVector.Builder("Media", 24.dp, 24.dp, 24f, 24f).apply {
        path(stroke = SolidColor(Color.White), strokeLineWidth = 2f) {
            moveTo(12f, 3f); arcTo(9f, 9f, 0f, true, true, 11.99f, 3f); close()
        }
        path(fill = SolidColor(Color.White)) {
            moveTo(10f, 8.5f); lineTo(16f, 12f); lineTo(10f, 15.5f); close()
        }
    }.build()
}
