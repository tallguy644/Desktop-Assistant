package com.tallguy644.syracompanion

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.basicMarquee
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
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.scale
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlin.math.sin
import kotlin.random.Random

// ---------- Boot sequence ----------
private val bootLines = listOf(
    "SYRA COMPANION // BUILD " + BuildConfig.VERSION_NAME.uppercase(),
    "INITIALIZING CORE ............ OK",
    "CALIBRATING REACTOR RINGS .... OK",
    "LOADING HUD MATRIX ........... OK",
    "OPENING SECURE UPLINK ........ OK",
    "ALL SYSTEMS NOMINAL",
)

/** A short start-up sequence. Tap anywhere to skip. */
@Composable
fun BootSequence(onDone: () -> Unit) {
    val shown = remember { mutableStateListOf<String>() }
    val grow = remember { Animatable(0.2f) }
    val progress = remember { Animatable(0f) }
    LaunchedEffect(Unit) {
        grow.animateTo(1f, tween(900, easing = FastOutSlowInEasing))
    }
    LaunchedEffect(Unit) {
        for ((i, line) in bootLines.withIndex()) {
            shown.add(line)
            progress.animateTo((i + 1f) / bootLines.size, tween(320))
        }
        delay(450)
        onDone()
    }
    HudBackground(Modifier.clickable(remember { MutableInteractionSource() }, indication = null) { onDone() }) {
        Column(Modifier.fillMaxSize().padding(28.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
            Reactor(230.dp, active = true, modifier = Modifier.scale(grow.value))
            Spacer(Modifier.height(24.dp))
            GlitchText("SYRA", fontSize = 34.sp, letterSpacing = 14.sp)
            Spacer(Modifier.height(24.dp))
            Column(Modifier.fillMaxWidth().height(130.dp)) {
                shown.forEachIndexed { i, line ->
                    Text(
                        "> $line", color = if (i == shown.lastIndex) Syra.Cyan else Syra.Muted,
                        fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 1.sp,
                    )
                }
            }
            SegmentBar(progress.value, color = Syra.Cyan, segments = 32)
            Spacer(Modifier.height(8.dp))
            HudLabel("TAP TO SKIP")
        }
    }
}

// ---------- Glitch title: occasional red/cyan split ----------
@Composable
fun GlitchText(text: String, modifier: Modifier = Modifier, fontSize: TextUnit = 18.sp, letterSpacing: TextUnit = 3.sp, color: Color = Syra.Ink) {
    var glitch by remember { mutableIntStateOf(0) }
    LaunchedEffect(text) {
        while (true) {
            delay(Random.nextLong(2200, 5200))
            repeat(3) { glitch = Random.nextInt(2, 6); delay(55); glitch = 0; delay(45) }
        }
    }
    Box(modifier) {
        if (glitch != 0) {
            Text(text, Modifier.offset { IntOffset(-glitch, 0) }, color = Syra.Core.copy(alpha = 0.75f), fontFamily = FontFamily.Monospace, fontSize = fontSize, letterSpacing = letterSpacing, fontWeight = FontWeight.Bold)
            Text(text, Modifier.offset { IntOffset(glitch, 0) }, color = Syra.Cyan.copy(alpha = 0.75f), fontFamily = FontFamily.Monospace, fontSize = fontSize, letterSpacing = letterSpacing, fontWeight = FontWeight.Bold)
        }
        Text(text, color = color, fontFamily = FontFamily.Monospace, fontSize = fontSize, letterSpacing = letterSpacing, fontWeight = FontWeight.Bold)
    }
}

// ---------- Live telemetry graph ----------
@Composable
fun Sparkline(series: List<Pair<List<Float>, Color>>, modifier: Modifier = Modifier, max: Float = 100f, capacity: Int = 40) {
    val t = rememberInfiniteTransition(label = "spark")
    val blink by t.animateFloat(0.3f, 1f, infiniteRepeatable(tween(700), RepeatMode.Reverse), label = "blink")
    Canvas(modifier.fillMaxWidth().height(120.dp)) {
        val grid = Syra.Cyan.copy(alpha = 0.08f)
        for (i in 0..4) { val y = size.height * i / 4f; drawLine(grid, Offset(0f, y), Offset(size.width, y), 1f) }
        for (i in 0..8) { val x = size.width * i / 8f; drawLine(grid, Offset(x, 0f), Offset(x, size.height), 1f) }
        val step = size.width / (capacity - 1)
        for ((values, color) in series) {
            if (values.size < 2) continue
            val start = capacity - values.size
            fun pt(i: Int) = Offset((start + i) * step, size.height - (values[i] / max).coerceIn(0f, 1f) * size.height)
            val line = Path().apply { moveTo(pt(0).x, pt(0).y); for (i in 1 until values.size) lineTo(pt(i).x, pt(i).y) }
            val area = Path().apply { addPath(line); lineTo(pt(values.lastIndex).x, size.height); lineTo(pt(0).x, size.height); close() }
            drawPath(area, Brush.verticalGradient(listOf(color.copy(alpha = 0.28f), Color.Transparent)))
            drawPath(line, color.copy(alpha = 0.25f), style = Stroke(7.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round))
            drawPath(line, color, style = Stroke(2.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round))
            val last = pt(values.lastIndex)
            drawCircle(color.copy(alpha = 0.35f * blink), 9.dp.toPx(), last)
            drawCircle(color, 3.dp.toPx(), last)
        }
    }
}

// ---------- Typewriter text with a blinking block cursor ----------
@Composable
fun Typewriter(text: String, modifier: Modifier = Modifier) {
    var count by remember(text) { mutableIntStateOf(0) }
    LaunchedEffect(text) {
        val perChar = (1400L / text.length.coerceAtLeast(1)).coerceIn(4L, 22L)
        while (count < text.length) { count = (count + 2).coerceAtMost(text.length); delay(perChar) }
    }
    val t = rememberInfiniteTransition(label = "cursor")
    val on by t.animateFloat(0f, 1f, infiniteRepeatable(tween(900), RepeatMode.Restart), label = "on")
    Text(text.take(count) + if (on < 0.5f) "▌" else " ", modifier, color = Syra.Ink, fontSize = 15.sp, lineHeight = 23.sp)
}

// ---------- Voice-style waveform used while Syra is working ----------
@Composable
fun Waveform(modifier: Modifier = Modifier, color: Color = Syra.Cyan, bars: Int = 28) {
    val t = rememberInfiniteTransition(label = "wave")
    val phase by t.animateFloat(0f, (2 * Math.PI).toFloat(), infiniteRepeatable(tween(1100, easing = LinearEasing)), label = "phase")
    Canvas(modifier.fillMaxWidth().height(44.dp)) {
        val w = size.width / bars
        for (i in 0 until bars) {
            val h = (0.25f + 0.75f * ((sin(phase + i * 0.55f) + 1f) / 2f) * ((sin(phase * 0.5f + i * 0.21f) + 1.3f) / 2.3f)) * size.height
            drawLine(color.copy(alpha = 0.85f), Offset(i * w + w / 2, (size.height - h) / 2), Offset(i * w + w / 2, (size.height + h) / 2), w * 0.45f, StrokeCap.Round)
        }
    }
}

// ---------- Scrolling telemetry ticker ----------
@OptIn(ExperimentalFoundationApi::class)
@Composable
fun Ticker(text: String, modifier: Modifier = Modifier) {
    Box(modifier.fillMaxWidth().hudFrame(Syra.Teal, fill = Syra.Panel.copy(alpha = 0.5f), cut = 6.dp).padding(vertical = 7.dp, horizontal = 10.dp)) {
        Text(text, Modifier.basicMarquee(iterations = Int.MAX_VALUE), color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 1.5.sp, maxLines = 1)
    }
}

// ---------- HUD navigation bar with a sliding bracket ----------
@Composable
fun HudNavBar(labels: List<String>, icons: List<ImageVector>, selected: Int, onSelect: (Int) -> Unit) {
    val haptics = LocalHapticFeedback.current
    val slide by animateFloatAsState(selected.toFloat(), tween(350, easing = FastOutSlowInEasing), label = "nav")
    Box(
        Modifier.fillMaxWidth()
            .drawBehind {
                drawRect(Brush.verticalGradient(listOf(Syra.Background.copy(alpha = 0.6f), Syra.Background)))
                drawLine(Syra.Cyan.copy(alpha = 0.45f), Offset(0f, 0f), Offset(size.width, 0f), 1.dp.toPx())
                val w = size.width / labels.size
                val x = slide * w
                drawRect(Brush.verticalGradient(listOf(Syra.Cyan.copy(alpha = 0.22f), Color.Transparent)), Offset(x + w * 0.12f, 0f), androidx.compose.ui.geometry.Size(w * 0.76f, size.height * 0.8f))
                drawLine(Syra.Cyan, Offset(x + w * 0.2f, 0f), Offset(x + w * 0.8f, 0f), 3.dp.toPx(), StrokeCap.Square)
            }
            .navigationBarsPadding(),
    ) {
        Row(Modifier.fillMaxWidth().height(64.dp)) {
            labels.forEachIndexed { i, label ->
                val on = i == selected
                Column(
                    Modifier.weight(1f).fillMaxSize().clickable(remember { MutableInteractionSource() }, indication = null) {
                        if (!on) haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove); onSelect(i)
                    },
                    horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center,
                ) {
                    Icon(icons[i], label, tint = if (on) Syra.Cyan else Syra.Muted, modifier = Modifier.size(22.dp))
                    Spacer(Modifier.height(4.dp))
                    Text(label.uppercase(), color = if (on) Syra.Ink else Syra.Muted, fontFamily = FontFamily.Monospace, fontSize = 10.sp, letterSpacing = 2.sp)
                }
            }
        }
    }
}
