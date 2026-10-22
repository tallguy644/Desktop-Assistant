package com.tallguy644.syracompanion

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.speech.RecognizerIntent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

// ---------------- Shared field ----------------
@Composable
private fun HudField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    placeholder: String = "",
    keyboardType: KeyboardType = KeyboardType.Text,
    imeAction: ImeAction = ImeAction.Next,
    onIme: () -> Unit = {},
    big: Boolean = false,
    singleLine: Boolean = true,
    secret: Boolean = false,
) {
    Column(modifier) {
        if (label.isNotEmpty()) { HudLabel(label.uppercase(), color = Syra.Cyan); Spacer(Modifier.height(6.dp)) }
        Box(Modifier.fillMaxWidth().hudFrame(Syra.Teal, fill = Color(0xCC07131F), cut = 8.dp).padding(horizontal = 14.dp, vertical = if (big) 14.dp else 13.dp)) {
            if (value.isEmpty() && placeholder.isNotEmpty()) Text(placeholder, color = Syra.Muted.copy(alpha = 0.6f), fontSize = 15.sp)
            BasicTextField(
                value = value, onValueChange = onValueChange, singleLine = singleLine, maxLines = if (singleLine) 1 else 4,
                textStyle = TextStyle(
                    color = Syra.Ink, fontFamily = FontFamily.Monospace,
                    fontSize = if (big) 26.sp else 15.sp, letterSpacing = if (big) 8.sp else 0.5.sp,
                ),
                cursorBrush = SolidColor(Syra.Cyan),
                visualTransformation = if (secret) PasswordVisualTransformation() else VisualTransformation.None,
                keyboardOptions = KeyboardOptions(keyboardType = keyboardType, imeAction = imeAction),
                keyboardActions = KeyboardActions(onDone = { onIme() }, onSend = { onIme() }, onSearch = { onIme() }),
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

@Composable
private fun Banner(text: String, accent: Color) {
    Row(Modifier.fillMaxWidth().hudFrame(accent, fill = accent.copy(alpha = 0.12f), cut = 8.dp).padding(12.dp), verticalAlignment = Alignment.CenterVertically) {
        Text("▲", color = accent, fontSize = 12.sp); Spacer(Modifier.width(10.dp))
        Text(text, color = Syra.Ink, fontSize = 13.sp)
    }
}

/** Animated "thinking" dots in HUD style. */
@Composable
private fun Working(text: String) {
    val t = rememberInfiniteTransition(label = "work")
    val phase by t.animateFloat(0f, 3f, infiniteRepeatable(tween(1200, easing = LinearEasing)), label = "phase")
    Row(verticalAlignment = Alignment.CenterVertically) {
        for (i in 0..2) {
            val on = phase.toInt() == i
            Box(Modifier.padding(end = 4.dp).size(width = 14.dp, height = 4.dp).hudFrame(if (on) Syra.Cyan else Syra.Teal, fill = if (on) Syra.Cyan else Color.Transparent, cut = 0.dp))
        }
        Spacer(Modifier.width(10.dp)); HudLabel(text, color = Syra.Cyan)
    }
}

// ---------------- Pairing ----------------
@Composable
fun PairScreen(vm: CompanionViewModel) {
    HudBackground {
        Column(
            Modifier.fillMaxSize().statusBarsPadding().verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 24.dp).imePadding(),
            verticalArrangement = Arrangement.spacedBy(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Reactor(170.dp, active = vm.pairBusy)
            Text("SYRA", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 30.sp, letterSpacing = 12.sp, fontWeight = FontWeight.Bold)
            HudLabel("COMPANION LINK // STANDBY", color = Syra.Cyan)
            Panel("ON YOUR PC") {
                Text(
                    "Open Syra → SETUP → Phone companion. Turn it on, SAVE, then PAIR NEW PHONE. " +
                        "Use the Tailscale address (100.x.x.x) to reach your PC from anywhere, or the home Wi-Fi address at home.",
                    color = Syra.Muted, fontSize = 14.sp, lineHeight = 20.sp,
                )
            }
            HudField(vm.pairHost, { vm.pairHost = it.trim() }, "PC address", Modifier.fillMaxWidth(), placeholder = "100.101.102.103", keyboardType = KeyboardType.Uri)
            HudField(vm.pairPort, { v -> vm.pairPort = v.filter { it.isDigit() }.take(5) }, "Port", Modifier.fillMaxWidth(), keyboardType = KeyboardType.Number)
            HudField(
                vm.pairCode, { v -> vm.pairCode = v.filter { it.isDigit() }.take(6) }, "Pairing code", Modifier.fillMaxWidth(),
                placeholder = "000000", keyboardType = KeyboardType.NumberPassword, imeAction = ImeAction.Done, onIme = { vm.pair() }, big = true,
            )
            vm.pairError?.let { Banner(it, Syra.Core) }
            HudButton("ESTABLISH LINK", { vm.pair() }, Modifier.fillMaxWidth(), busy = vm.pairBusy)
            HudButton("TRY DEMO", { vm.startDemo() }, Modifier.fillMaxWidth(), enabled = !vm.pairBusy, primary = false)
            Text("Explore the app with sample data. No PC needed; nothing is sent anywhere.", color = Syra.Muted, fontSize = 13.sp, textAlign = TextAlign.Center)
            HudLabel("BUILD ${BuildConfig.VERSION_NAME.uppercase()}")
        }
    }
}

// ---------------- Dashboard ----------------
@Composable
fun DashboardTab(vm: CompanionViewModel) {
    val s = vm.status
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        if (vm.demo) Banner("DEMO MODE — sample data, nothing is sent to a PC. Exit from the ⋮ menu.", Syra.Cyan)
        vm.connectionError?.let { Banner(it, Syra.Core) }
        Reactor(210.dp, active = s?.listening == true)
        Text(
            s?.state?.ifBlank { null } ?: if (s == null) "LINKING" else "ONLINE",
            color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 18.sp, letterSpacing = 4.sp, fontWeight = FontWeight.Bold,
        )
        HudLabel(
            when { s == null -> "AWAITING TELEMETRY"; s.listening -> "MIC ● LISTENING"; else -> "MIC ○ STANDBY" },
            color = if (s?.listening == true) Syra.Good else Syra.Muted,
        )
        if (s == null) return@Column
        Panel("SYSTEM LOAD") {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                ArcGauge("CPU", s.cpu, Modifier.weight(1f))
                ArcGauge("RAM", s.ram, Modifier.weight(1f))
                ArcGauge("GPU", s.gpu, Modifier.weight(1f))
            }
            if (s.memory.isNotBlank() && s.memory != "Unavailable") { Spacer(Modifier.height(8.dp)); HudLabel(s.memory.uppercase()) }
        }
        Panel("THERMALS") {
            Row(Modifier.fillMaxWidth()) {
                Reading("CPU CORE", s.cpuTempF, Modifier.weight(1f))
                Reading("GPU CORE", s.gpuTempF, Modifier.weight(1f))
            }
            if (s.fans.isNotEmpty()) {
                Spacer(Modifier.height(14.dp))
                val top = (s.fans.maxOf { it.second }).coerceAtLeast(1)
                s.fans.forEach { (name, rpm) ->
                    Column(Modifier.padding(vertical = 4.dp)) {
                        Row(Modifier.fillMaxWidth()) {
                            HudLabel(name.uppercase(), Modifier.weight(1f))
                            Text("$rpm RPM", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 12.sp)
                        }
                        Spacer(Modifier.height(4.dp))
                        SegmentBar(rpm.toFloat() / (top * 1.15f), color = Syra.Teal)
                    }
                }
            }
            if (s.cpuTempF == null && s.gpuTempF == null && s.sensorStatus.isNotBlank()) {
                Spacer(Modifier.height(8.dp)); Text(s.sensorStatus, color = Syra.Muted, fontSize = 12.sp)
            }
        }
        Panel("DISPLAYS", accent = if (s.screensOff) Syra.Warn else Syra.Cyan) {
            Text(
                when {
                    !s.screensEnabled -> "Turn on Screens & voice wake in Syra Setup to control displays from here."
                    !s.screensActive -> "Syra needs to be listening (LISTEN) to control screens."
                    s.screensOff -> "Displays offline. Voice wake armed."
                    else -> "Displays online."
                },
                color = Syra.Muted, fontSize = 13.sp,
            )
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                HudButton("WAKE", { vm.screens("wake") }, Modifier.weight(1f), enabled = s.screensActive)
                HudButton("OFF", { vm.screens("off") }, Modifier.weight(1f), enabled = s.screensActive, primary = false, accent = Syra.Warn)
            }
        }
        Spacer(Modifier.height(8.dp))
    }
}

@Composable
private fun Reading(label: String, tempF: Double?, modifier: Modifier) {
    val color = when { tempF == null -> Syra.Muted; tempF >= 185 -> Syra.Core; tempF >= 160 -> Syra.Warn; else -> Syra.Cyan }
    Column(modifier) {
        HudLabel(label)
        Row(verticalAlignment = Alignment.Bottom) {
            Text(tempF?.let { String.format(Locale.US, "%.0f", it) } ?: "—", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 38.sp, fontWeight = FontWeight.Light)
            Text("°F", color = color, fontFamily = FontFamily.Monospace, fontSize = 14.sp, modifier = Modifier.padding(start = 4.dp, bottom = 8.dp))
        }
        SegmentBar(((tempF ?: 0.0) / 212.0).toFloat(), Modifier.width(110.dp), color = color, segments = 14)
    }
}

// ---------------- Apps ----------------
@Composable
fun AppsTab(vm: CompanionViewModel) {
    var filter by remember { mutableStateOf("") }
    val shown = vm.apps.filter { it.contains(filter.trim(), ignoreCase = true) }
    Column(Modifier.fillMaxSize().padding(horizontal = 16.dp)) {
        Spacer(Modifier.height(12.dp))
        HudField(filter, { filter = it }, "", Modifier.fillMaxWidth(), placeholder = "⌕  SEARCH APPS", imeAction = ImeAction.Search)
        Spacer(Modifier.height(10.dp))
        HudLabel("${shown.size} AUTHORIZED APPLICATIONS", color = Syra.Cyan)
        Spacer(Modifier.height(10.dp))
        when {
            vm.appsLoading && vm.apps.isEmpty() -> Box(Modifier.fillMaxWidth().padding(40.dp), contentAlignment = Alignment.Center) { Working("SCANNING") }
            vm.apps.isEmpty() -> Text("No apps yet. Add mappings in Syra Setup → Applications & mappings, then use Refresh apps.", color = Syra.Muted)
            else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                itemsIndexed(shown, key = { _, name -> name }) { i, name ->
                    Row(
                        Modifier.fillMaxWidth().hudFrame(Syra.Teal, cut = 10.dp).clickable { vm.launch(name) }.padding(horizontal = 16.dp, vertical = 15.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(String.format(Locale.US, "%02d", i + 1), color = Syra.Cyan.copy(alpha = 0.6f), fontFamily = FontFamily.Monospace, fontSize = 12.sp)
                        Spacer(Modifier.width(14.dp))
                        Text(name.uppercase(), color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 14.sp, letterSpacing = 1.5.sp, modifier = Modifier.weight(1f))
                        Text("LAUNCH ›", color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 1.5.sp)
                    }
                }
                item { Spacer(Modifier.height(12.dp)) }
            }
        }
    }
}

// ---------------- Ask ----------------
@Composable
fun AskTab(vm: CompanionViewModel) {
    val speech = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        if (result.resultCode == Activity.RESULT_OK) {
            val text = result.data?.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS)?.firstOrNull()
            if (!text.isNullOrBlank()) { vm.question = text; vm.ask() }
        }
    }
    Column(Modifier.fillMaxSize().padding(16.dp).imePadding(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            HudButton("AGENDA", { vm.readAgenda() }, Modifier.weight(1f), enabled = !vm.asking, primary = false)
            HudButton("COMMUTE", { vm.readCommute() }, Modifier.weight(1f), enabled = !vm.asking, primary = false)
        }
        Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()), horizontalAlignment = Alignment.CenterHorizontally) {
            if (vm.asking || vm.answer != null || vm.agenda.isNotEmpty()) {
                Panel(vm.answerTitle.uppercase().take(60)) {
                    if (vm.asking) Working("PROCESSING")
                    vm.agenda.forEach { item ->
                        Row(Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
                            Text(item.time, color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 12.sp, modifier = Modifier.width(84.dp).padding(top = 2.dp))
                            Column(Modifier.weight(1f)) {
                                Text(item.title, color = Syra.Ink, fontSize = 15.sp)
                                if (item.location.isNotBlank()) HudLabel("◆ " + item.location.uppercase())
                            }
                        }
                    }
                    vm.answer?.takeIf { it.isNotBlank() }?.let { Text(it, color = Syra.Ink, fontSize = 15.sp, lineHeight = 23.sp) }
                }
            } else {
                Spacer(Modifier.height(12.dp))
                Reactor(150.dp, active = false)
                Spacer(Modifier.height(12.dp))
                val ready = vm.status?.chatReady != false
                Text(
                    if (ready) "Ask Syra anything. Answers come from the ChatGPT account connected on your PC."
                    else "Connect ChatGPT in Syra Setup on your PC to ask questions. Agenda and commute still work.",
                    color = Syra.Muted, fontSize = 14.sp, textAlign = TextAlign.Center,
                )
            }
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            HudLabel("VOICE OUTPUT ON PC", Modifier.weight(1f))
            Switch(
                checked = vm.speakOnPc, onCheckedChange = { vm.speakOnPc = it },
                colors = SwitchDefaults.colors(checkedTrackColor = Syra.Teal, checkedThumbColor = Syra.Cyan, uncheckedTrackColor = Syra.PanelHigh, uncheckedThumbColor = Syra.Muted, uncheckedBorderColor = Syra.Teal),
            )
        }
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            HudField(vm.question, { vm.question = it }, "", Modifier.weight(1f), placeholder = "ASK SYRA…", imeAction = ImeAction.Send, onIme = { vm.ask() }, singleLine = false)
            MicButton(enabled = !vm.asking) {
                val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                    .putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                    .putExtra(RecognizerIntent.EXTRA_PROMPT, "Ask Syra")
                try { speech.launch(intent) } catch (e: ActivityNotFoundException) { vm.toast = "No speech recognizer on this phone. Install or enable Google voice typing." }
            }
        }
        HudButton("TRANSMIT", { vm.ask() }, Modifier.fillMaxWidth(), enabled = !vm.asking && vm.question.isNotBlank())
    }
}

/** Round mic button with a breathing red ring, like the reactor core. */
@Composable
private fun MicButton(enabled: Boolean, onClick: () -> Unit) {
    val t = rememberInfiniteTransition(label = "mic")
    val pulse by t.animateFloat(0f, 1f, infiniteRepeatable(tween(1800), RepeatMode.Restart), label = "micPulse")
    Box(Modifier.size(58.dp).clickable(enabled = enabled, onClick = onClick), contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val r = size.minDimension / 2
            if (enabled) drawCircle(Syra.Core.copy(alpha = (1f - pulse) * 0.5f), r * (0.7f + pulse * 0.3f), style = Stroke(2.dp.toPx()))
            drawCircle(if (enabled) Syra.Core.copy(alpha = 0.9f) else Syra.PanelHigh, r * 0.66f)
            drawCircle(Color.White.copy(alpha = 0.25f), r * 0.66f, style = Stroke(1.dp.toPx()))
        }
        androidx.compose.material3.Icon(MicIcon, "Speak a question", tint = Color.White, modifier = Modifier.size(24.dp))
    }
}
