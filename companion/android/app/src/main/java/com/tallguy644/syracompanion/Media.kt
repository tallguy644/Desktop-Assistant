package com.tallguy644.syracompanion

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlin.math.sin

private val SpotifyGreen = Color(0xFF1DB954)
private val YouTubeRed = Color(0xFFFF2E2E)

private enum class Glyph { Play, Pause, Next, Previous }

@Composable
fun MediaTab(vm: CompanionViewModel) {
    val now = vm.status?.nowPlaying
    val playing = now?.playing == true
    val accent = when (now?.source?.lowercase()) { "spotify" -> SpotifyGreen; "youtube" -> YouTubeRed; else -> Syra.Cyan }
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Panel("NOW PLAYING", accent = accent) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(Modifier.hudFrame(accent, fill = accent.copy(alpha = 0.18f), cut = 5.dp).padding(horizontal = 10.dp, vertical = 4.dp)) {
                    Text((now?.source ?: "NO SOURCE").uppercase(), color = accent, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 2.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.weight(1f))
                HudLabel(if (now == null) "IDLE" else if (playing) "● PLAYING" else "❚❚ PAUSED", color = if (playing) accent else Syra.Muted)
            }
            Spacer(Modifier.height(14.dp))
            val (artist, song) = splitTitle(now?.title)
            Text(song ?: "Nothing detected", color = Syra.Ink, fontSize = 22.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
            if (artist != null) Text(artist.uppercase(), color = Syra.Muted, fontFamily = FontFamily.Monospace, fontSize = 12.sp, letterSpacing = 1.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Spacer(Modifier.height(14.dp))
            Equalizer(playing, accent)
        }

        Row(Modifier.fillMaxWidth().padding(vertical = 8.dp), horizontalArrangement = Arrangement.SpaceEvenly, verticalAlignment = Alignment.CenterVertically) {
            RoundControl(Glyph.Previous, 64.dp, Syra.Teal, spinning = false) { vm.media("previous") }
            RoundControl(if (playing) Glyph.Pause else Glyph.Play, 112.dp, accent, spinning = playing) { vm.media("playpause") }
            RoundControl(Glyph.Next, 64.dp, Syra.Teal, spinning = false) { vm.media("next") }
        }

        Panel("VOLUME // PC") {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                HudButton("VOL −", { vm.media("voldown") }, Modifier.weight(1f), primary = false)
                HudButton("MUTE", { vm.media("mute") }, Modifier.weight(1f), primary = false, accent = Syra.Warn)
                HudButton("VOL +", { vm.media("volup") }, Modifier.weight(1f), primary = false)
            }
        }
        Text(
            "Controls work like your keyboard's media keys: Spotify, YouTube in Chrome or Edge, and most players respond to them. " +
                "Now playing is read from the Spotify window or the browser tab that's showing YouTube.",
            color = Syra.Muted, fontSize = 12.sp, textAlign = TextAlign.Center, lineHeight = 17.sp,
        )
    }
}

/** "Artist - Song" (Spotify) or "Video title" (YouTube). */
private fun splitTitle(title: String?): Pair<String?, String?> {
    if (title.isNullOrBlank()) return null to null
    val i = title.indexOf(" - ")
    return if (i > 0) title.substring(0, i) to title.substring(i + 3) else null to title
}

@Composable
private fun Equalizer(playing: Boolean, color: Color, bars: Int = 32) {
    val t = rememberInfiniteTransition(label = "eq")
    val phase by t.animateFloat(0f, 6.2832f, infiniteRepeatable(tween(900, easing = LinearEasing)), label = "eqPhase")
    Canvas(Modifier.fillMaxWidth().height(36.dp)) {
        val w = size.width / bars
        for (i in 0 until bars) {
            val level = if (playing) 0.15f + 0.85f * ((sin(phase * (1 + i % 3) + i * 0.7f) + 1f) / 2f) * ((sin(phase + i * 0.33f) + 1.4f) / 2.4f) else 0.08f
            val h = size.height * level
            drawRect(
                Brush.verticalGradient(listOf(color, color.copy(alpha = 0.25f)), startY = size.height - h, endY = size.height),
                topLeft = Offset(i * w + w * 0.18f, size.height - h),
                size = androidx.compose.ui.geometry.Size(w * 0.64f, h),
            )
        }
    }
}

@Composable
private fun RoundControl(glyph: Glyph, diameter: Dp, accent: Color, spinning: Boolean, onClick: () -> Unit) {
    val haptics = LocalHapticFeedback.current
    val t = rememberInfiniteTransition(label = "ctl")
    val spin by t.animateFloat(0f, 360f, infiniteRepeatable(tween(6000, easing = LinearEasing)), label = "spin")
    Box(
        Modifier.size(diameter).clickable(remember { MutableInteractionSource() }, indication = null) {
            haptics.performHapticFeedback(HapticFeedbackType.LongPress); onClick()
        },
        contentAlignment = Alignment.Center,
    ) {
        Canvas(Modifier.fillMaxSize()) {
            val r = size.minDimension / 2
            drawCircle(Brush.radialGradient(listOf(accent.copy(alpha = 0.28f), Color.Transparent), center, r), r)
            rotate(if (spinning) spin else 0f) {
                drawCircle(accent.copy(alpha = 0.85f), r * 0.92f, style = Stroke(2.dp.toPx(), pathEffect = PathEffect.dashPathEffect(floatArrayOf(r * 0.5f, r * 0.18f))))
            }
            drawCircle(Syra.Panel.copy(alpha = 0.9f), r * 0.78f)
            drawCircle(accent.copy(alpha = 0.6f), r * 0.78f, style = Stroke(1.dp.toPx()))
            drawGlyph(glyph, r * 0.34f, accent)
        }
    }
}

private fun DrawScope.drawGlyph(glyph: Glyph, s: Float, color: Color) {
    val c = center
    fun tri(left: Float, pointRight: Boolean) = Path().apply {
        if (pointRight) { moveTo(left, c.y - s); lineTo(left + s * 1.5f, c.y); lineTo(left, c.y + s) }
        else { moveTo(left + s * 1.5f, c.y - s); lineTo(left, c.y); lineTo(left + s * 1.5f, c.y + s) }
        close()
    }
    when (glyph) {
        Glyph.Play -> drawPath(tri(c.x - s * 0.6f, true), color)
        Glyph.Pause -> {
            drawLine(color, Offset(c.x - s * 0.45f, c.y - s), Offset(c.x - s * 0.45f, c.y + s), s * 0.5f, StrokeCap.Square)
            drawLine(color, Offset(c.x + s * 0.45f, c.y - s), Offset(c.x + s * 0.45f, c.y + s), s * 0.5f, StrokeCap.Square)
        }
        Glyph.Next -> {
            drawPath(tri(c.x - s * 1.0f, true), color)
            drawLine(color, Offset(c.x + s * 0.75f, c.y - s), Offset(c.x + s * 0.75f, c.y + s), s * 0.35f, StrokeCap.Square)
        }
        Glyph.Previous -> {
            drawPath(tri(c.x - s * 0.5f, false), color)
            drawLine(color, Offset(c.x - s * 0.75f, c.y - s), Offset(c.x - s * 0.75f, c.y + s), s * 0.35f, StrokeCap.Square)
        }
    }
}
