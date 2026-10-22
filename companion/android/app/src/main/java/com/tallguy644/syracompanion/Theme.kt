package com.tallguy644.syracompanion

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.path
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp

// Syra HUD palette: deep navy glass, cyan rings, red reactor core.
object Syra {
    val Background = Color(0xFF060D18)
    val Panel = Color(0xFF0A1A28)
    val PanelHigh = Color(0xFF0F2536)
    val Cyan = Color(0xFF4FD8FF)
    val Teal = Color(0xFF1C8FB8)
    val Core = Color(0xFFFF3B3B)
    val Ink = Color(0xFFE6F4FF)
    val Muted = Color(0xFF7FA3B8)
    val Good = Color(0xFF39D98A)
    val Warn = Color(0xFFFFB547)
}

private val scheme = darkColorScheme(
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

@Composable
fun SyraTheme(content: @Composable () -> Unit) {
    val base = Typography()
    val mono = FontFamily.Monospace
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
