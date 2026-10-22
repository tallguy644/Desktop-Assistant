package com.tallguy644.syracompanion

import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlin.math.cos
import kotlin.math.min
import kotlin.math.sin
import androidx.compose.animation.core.Animatable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import kotlinx.coroutines.delay
import kotlin.random.Random

// ---------- Background: deep navy, faint grid, slow scan line ----------
@Composable
fun HudBackground(modifier: Modifier = Modifier, content: @Composable BoxScope.() -> Unit) {
    val t = rememberInfiniteTransition(label = "bg")
    val scan by t.animateFloat(0f, 1f, infiniteRepeatable(tween(7000, easing = LinearEasing)), label = "scan")
    val drift by t.animateFloat(0f, 1f, infiniteRepeatable(tween(40000, easing = LinearEasing)), label = "drift")
    // x, y, speed, size: tiny glowing motes that rise slowly through the grid
    val motes = remember { List(36) { floatArrayOf(Random.nextFloat(), Random.nextFloat(), 0.3f + Random.nextFloat() * 0.9f, 1f + Random.nextFloat() * 2.2f) } }
    Box(
        modifier
            .fillMaxSize()
            .drawBehind {
                drawRect(Brush.verticalGradient(listOf(Color(0xFF071526), Syra.Background, Color(0xFF03070E))))
                val step = 28.dp.toPx()
                val grid = Syra.Cyan.copy(alpha = 0.045f)
                var x = 0f
                while (x < size.width) { drawLine(grid, Offset(x, 0f), Offset(x, size.height), 1f); x += step }
                var y = 0f
                while (y < size.height) { drawLine(grid, Offset(0f, y), Offset(size.width, y), 1f); y += step }
                for (m in motes) {
                    val py = ((m[1] - drift * m[2] * 2f) % 1f + 1f) % 1f
                    val px = m[0] * size.width + sin((drift * 6.283f * m[2] * 3f) + m[0] * 10f) * 18f
                    val twinkle = 0.35f + 0.65f * ((sin(drift * 60f * m[2] + m[1] * 20f) + 1f) / 2f)
                    drawCircle(Syra.Cyan.copy(alpha = 0.10f * twinkle), m[3] * 3.2f, Offset(px, py * size.height))
                    drawCircle(Syra.Cyan.copy(alpha = 0.55f * twinkle), m[3], Offset(px, py * size.height))
                }
                val sy = scan * size.height
                drawRect(
                    Brush.verticalGradient(listOf(Color.Transparent, Syra.Cyan.copy(alpha = 0.07f), Color.Transparent), startY = sy - 60f, endY = sy + 60f),
                    topLeft = Offset(0f, sy - 60f), size = Size(size.width, 120f),
                )
                drawRect(Brush.radialGradient(listOf(Color.Transparent, Color(0xAA000000)), center = center, radius = size.maxDimension * 0.75f))
            },
        content = content,
    )
}

// ---------- Panel: cut-corner glass with glowing brackets ----------
private fun DrawScope.cutPath(cut: Float): Path = Path().apply {
    moveTo(cut, 0f); lineTo(size.width, 0f); lineTo(size.width, size.height - cut)
    lineTo(size.width - cut, size.height); lineTo(0f, size.height); lineTo(0f, cut); close()
}

fun Modifier.hudFrame(accent: Color = Syra.Cyan, fill: Color = Syra.Panel.copy(alpha = 0.72f), cut: Dp = 14.dp): Modifier = drawBehind {
    val c = cut.toPx()
    val path = cutPath(c)
    drawPath(path, Brush.verticalGradient(listOf(fill, fill.copy(alpha = fill.alpha * 0.55f))))
    drawPath(path, accent.copy(alpha = 0.10f), style = Stroke(6.dp.toPx()))
    drawPath(path, accent.copy(alpha = 0.38f), style = Stroke(1.dp.toPx()))
    val w = 2.5.dp.toPx(); val len = 26.dp.toPx()
    // top-left bracket runs along the cut corner
    drawLine(accent, Offset(c, 0f), Offset(c + len, 0f), w, StrokeCap.Square)
    drawLine(accent, Offset(0f, c), Offset(c, 0f), w, StrokeCap.Square)
    drawLine(accent, Offset(0f, c), Offset(0f, c + len * 0.6f), w, StrokeCap.Square)
    // bottom-right bracket
    drawLine(accent, Offset(size.width - c - len, size.height), Offset(size.width - c, size.height), w, StrokeCap.Square)
    drawLine(accent, Offset(size.width - c, size.height), Offset(size.width, size.height - c), w, StrokeCap.Square)
    // tiny ticks on the right edge
    for (i in 0..2) { val ty = c + 10.dp.toPx() + i * 6.dp.toPx(); drawLine(accent.copy(alpha = 0.6f), Offset(size.width - 6.dp.toPx(), ty), Offset(size.width, ty), 1.5f) }
}

@Composable
fun Panel(title: String, modifier: Modifier = Modifier, accent: Color = Syra.Cyan, content: @Composable ColumnScope.() -> Unit) {
    val t = rememberInfiniteTransition(label = "panel")
    val sheen by t.animateFloat(-0.6f, 1.6f, infiniteRepeatable(tween(5600, easing = LinearEasing)), label = "sheen")
    val appear = remember { Animatable(0f) }
    LaunchedEffect(Unit) { appear.snapTo(0.55f); delay(70); appear.snapTo(0.12f); delay(60); appear.snapTo(0.8f); delay(50); appear.animateTo(1f, tween(220)) }
    Column(
        modifier.fillMaxWidth()
            .graphicsLayer { alpha = appear.value }
            .hudFrame(accent)
            .drawWithContent {
                drawContent()
                val x = sheen * size.width
                drawRect(Brush.linearGradient(listOf(Color.Transparent, accent.copy(alpha = 0.07f), Color.Transparent), start = Offset(x - 140f, 0f), end = Offset(x + 140f, size.height)))
            }
            .padding(horizontal = 18.dp, vertical = 16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(width = 3.dp, height = 12.dp).drawBehind { drawRect(accent) })
            Spacer(Modifier.width(8.dp))
            Text(title, color = accent, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 2.5.sp)
            Spacer(Modifier.weight(1f))
            Text("//", color = accent.copy(alpha = 0.35f), fontFamily = FontFamily.Monospace, fontSize = 11.sp)
        }
        Spacer(Modifier.height(12.dp))
        content()
    }
}

// ---------- Buttons ----------
@Composable
fun HudButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true, primary: Boolean = true, accent: Color = Syra.Cyan, busy: Boolean = false) {
    val color = if (enabled) accent else Syra.Muted.copy(alpha = 0.5f)
    val haptics = LocalHapticFeedback.current
    Box(
        modifier
            .height(52.dp)
            .hudFrame(color, fill = if (primary && enabled) accent.copy(alpha = 0.22f) else Syra.Panel.copy(alpha = 0.5f), cut = 10.dp)
            .clickable(enabled = enabled && !busy) { haptics.performHapticFeedback(HapticFeedbackType.LongPress); onClick() },
        contentAlignment = Alignment.Center,
    ) {
        Text(
            if (busy) "· · ·" else text, color = if (enabled) Syra.Ink else Syra.Muted,
            fontFamily = FontFamily.Monospace, fontSize = 14.sp, letterSpacing = 3.sp, fontWeight = FontWeight.Bold,
        )
    }
}

// ---------- Status dot with a pulse ----------
@Composable
fun StatusDot(online: Boolean) {
    val t = rememberInfiniteTransition(label = "dot")
    val pulse by t.animateFloat(0f, 1f, infiniteRepeatable(tween(1600), RepeatMode.Restart), label = "pulse")
    val color = if (online) Syra.Good else Syra.Core
    Canvas(Modifier.padding(horizontal = 10.dp).size(18.dp)) {
        val r = size.minDimension / 2
        drawCircle(color.copy(alpha = (1f - pulse) * 0.45f), radius = r * (0.35f + pulse * 0.65f))
        drawCircle(color, radius = r * 0.32f)
    }
}

// ---------- Reactor: rotating rings, ticks and a pulsing red core ----------
@Composable
fun Reactor(diameter: Dp, active: Boolean, modifier: Modifier = Modifier, center: @Composable BoxScope.() -> Unit = {}) {
    val t = rememberInfiniteTransition(label = "reactor")
    val slow by t.animateFloat(0f, 360f, infiniteRepeatable(tween(if (active) 14000 else 26000, easing = LinearEasing)), label = "slow")
    val fast by t.animateFloat(360f, 0f, infiniteRepeatable(tween(if (active) 5000 else 11000, easing = LinearEasing)), label = "fast")
    val sweep by t.animateFloat(0f, 360f, infiniteRepeatable(tween(if (active) 2600 else 4200, easing = LinearEasing)), label = "sweep")
    val beat by t.animateFloat(0.75f, 1f, infiniteRepeatable(tween(if (active) 900 else 1800, easing = FastOutSlowInEasing), RepeatMode.Reverse), label = "beat")
    Box(modifier.size(diameter), contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val r = size.minDimension / 2
            val c = this.center
            drawCircle(Brush.radialGradient(listOf(Syra.Cyan.copy(alpha = 0.16f), Color.Transparent), c, r), r)
            // outer tick ring
            for (i in 0 until 72) {
                val a = Math.toRadians(i * 5.0)
                val inner = if (i % 6 == 0) r * 0.88f else r * 0.92f
                drawLine(Syra.Cyan.copy(alpha = if (i % 6 == 0) 0.8f else 0.3f),
                    Offset(c.x + cos(a).toFloat() * inner, c.y + sin(a).toFloat() * inner),
                    Offset(c.x + cos(a).toFloat() * r * 0.97f, c.y + sin(a).toFloat() * r * 0.97f), 1.5f)
            }
            // radar sweep
            rotate(sweep, c) {
                drawArc(
                    Brush.sweepGradient(0f to Color.Transparent, 0.80f to Color.Transparent, 1f to Syra.Cyan.copy(alpha = 0.38f), center = c),
                    -72f, 72f, true, Offset(c.x - r * 0.95f, c.y - r * 0.95f), Size(r * 1.9f, r * 1.9f),
                )
                drawLine(Syra.Cyan.copy(alpha = 0.7f), c, Offset(c.x + r * 0.95f, c.y), 1.5f)
            }
            rotate(slow, c) {
                ring(r * 0.80f, 3.dp.toPx(), Syra.Cyan.copy(alpha = 0.85f), floatArrayOf(r * 0.9f, r * 0.25f))
                ring(r * 0.72f, 1.dp.toPx(), Syra.Teal, floatArrayOf(6f, 10f))
            }
            rotate(fast, c) {
                ring(r * 0.62f, 6.dp.toPx(), Syra.Teal.copy(alpha = 0.9f), floatArrayOf(r * 0.35f, r * 0.18f))
                ring(r * 0.52f, 1.5.dp.toPx(), Syra.Cyan.copy(alpha = 0.6f), floatArrayOf(r * 1.4f, r * 0.4f))
            }
            // red core
            drawCircle(Brush.radialGradient(listOf(Syra.Core.copy(alpha = 0.55f * beat), Color.Transparent), c, r * 0.48f), r * 0.48f)
            drawCircle(Syra.Core.copy(alpha = 0.95f), r * 0.17f * beat, c)
            drawCircle(Color.White.copy(alpha = 0.55f), r * 0.06f * beat, c)
            drawCircle(Syra.Core.copy(alpha = 0.7f), r * 0.26f, c, style = Stroke(1.5.dp.toPx()))
        }
        center()
    }
}

private fun DrawScope.ring(radius: Float, width: Float, color: Color, dash: FloatArray) {
    drawCircle(color, radius, center, style = Stroke(width, pathEffect = PathEffect.dashPathEffect(dash)))
}

// ---------- Arc gauge ----------
@Composable
fun ArcGauge(label: String, value: Double?, modifier: Modifier = Modifier, suffix: String = "%", max: Double = 100.0, warnAt: Double = 75.0, hotAt: Double = 90.0) {
    val target = ((value ?: 0.0) / max).toFloat().coerceIn(0f, 1f)
    val shown by animateFloatAsState(target, tween(900, easing = FastOutSlowInEasing), label = "gauge")
    val v = value ?: 0.0
    val color = when { value == null -> Syra.Muted; v >= hotAt -> Syra.Core; v >= warnAt -> Syra.Warn; else -> Syra.Cyan }
    Column(modifier, horizontalAlignment = Alignment.CenterHorizontally) {
        Box(Modifier.size(96.dp), contentAlignment = Alignment.Center) {
            Canvas(Modifier.fillMaxSize()) {
                val stroke = 7.dp.toPx()
                val d = min(size.width, size.height) - stroke * 2
                val tl = Offset((size.width - d) / 2, (size.height - d) / 2)
                drawArc(Syra.PanelHigh, 135f, 270f, false, tl, Size(d, d), style = Stroke(stroke, cap = StrokeCap.Round))
                drawArc(color.copy(alpha = 0.25f), 135f, 270f * shown, false, tl, Size(d, d), style = Stroke(stroke * 2.2f, cap = StrokeCap.Round))
                drawArc(Brush.sweepGradient(listOf(Syra.Teal, color, Syra.Teal)), 135f, 270f * shown, false, tl, Size(d, d), style = Stroke(stroke, cap = StrokeCap.Round))
                val r = d / 2 + stroke * 1.3f
                for (i in 0..10) {
                    val a = Math.toRadians(135.0 + i * 27.0)
                    drawLine(Syra.Muted.copy(alpha = 0.5f),
                        Offset(center.x + cos(a).toFloat() * r, center.y + sin(a).toFloat() * r),
                        Offset(center.x + cos(a).toFloat() * (r + 4f), center.y + sin(a).toFloat() * (r + 4f)), 2f)
                }
            }
            Text(value?.let { "${it.toInt()}$suffix" } ?: "—", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 18.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
        }
        Text(label, color = color, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 2.sp)
    }
}

// ---------- Thin segmented bar ----------
@Composable
fun SegmentBar(fraction: Float, modifier: Modifier = Modifier, color: Color = Syra.Cyan, segments: Int = 24) {
    val shown by animateFloatAsState(fraction.coerceIn(0f, 1f), tween(700), label = "bar")
    Canvas(modifier.fillMaxWidth().height(8.dp)) {
        val gap = 2.dp.toPx(); val w = (size.width - gap * (segments - 1)) / segments
        val lit = (shown * segments).toInt()
        for (i in 0 until segments) {
            drawRect(if (i < lit) color else Syra.PanelHigh, Offset(i * (w + gap), 0f), Size(w, size.height))
        }
    }
}

@Composable
fun HudLabel(text: String, modifier: Modifier = Modifier, color: Color = Syra.Muted) =
    Text(text, modifier, color = color, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 1.5.sp)
