package com.tallguy644.syracompanion

import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.speech.RecognizerIntent
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.CutCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

private val PanelShape = CutCornerShape(topStart = 14.dp, bottomEnd = 14.dp)

@Composable
fun Panel(title: String, modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = PanelShape,
        color = Syra.Panel,
        border = BorderStroke(1.dp, Color(0xFF1D5A70)),
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(title, color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 12.sp, letterSpacing = 2.sp)
            Spacer(Modifier.height(10.dp))
            content()
        }
    }
}

@Composable
fun StatusDot(online: Boolean) {
    Box(
        Modifier
            .padding(horizontal = 8.dp)
            .size(10.dp)
            .clip(CircleShape)
            .background(if (online) Syra.Good else Syra.Core)
    )
}

// ---------------- Pairing ----------------
@Composable
fun PairScreen(vm: CompanionViewModel) {
    Column(
        Modifier
            .fillMaxSize()
            .background(Syra.Background)
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 48.dp)
            .imePadding(),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text("SYRA / COMPANION", color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 22.sp, letterSpacing = 3.sp)
        Text("Pair with Syra on your PC.", color = Syra.Ink, fontSize = 16.sp)
        Panel("ON YOUR PC") {
            Text(
                "Open Syra → SETUP → Phone companion. Turn it on, SAVE, then choose PAIR NEW PHONE. " +
                    "Use the Tailscale address (100.x.x.x) to reach your PC from anywhere, or the home Wi-Fi address when you're home.",
                color = Syra.Muted, fontSize = 14.sp,
            )
        }
        OutlinedTextField(
            value = vm.pairHost, onValueChange = { vm.pairHost = it.trim() },
            label = { Text("PC address") }, placeholder = { Text("100.101.102.103 or my-pc") },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri, imeAction = ImeAction.Next),
        )
        OutlinedTextField(
            value = vm.pairPort, onValueChange = { v -> vm.pairPort = v.filter { it.isDigit() }.take(5) },
            label = { Text("Port") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = ImeAction.Next),
        )
        OutlinedTextField(
            value = vm.pairCode, onValueChange = { v -> vm.pairCode = v.filter { it.isDigit() }.take(6) },
            label = { Text("6-digit pairing code") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
            textStyle = androidx.compose.ui.text.TextStyle(fontFamily = FontFamily.Monospace, fontSize = 22.sp, letterSpacing = 6.sp, color = Syra.Ink),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword, imeAction = ImeAction.Done),
            keyboardActions = KeyboardActions(onDone = { vm.pair() }),
        )
        vm.pairError?.let { Text(it, color = Syra.Core, fontSize = 14.sp) }
        Button(
            onClick = { vm.pair() }, enabled = !vm.pairBusy,
            modifier = Modifier.fillMaxWidth().height(52.dp), shape = PanelShape,
        ) {
            if (vm.pairBusy) CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp, color = Syra.Background)
            else Text("PAIR", fontFamily = FontFamily.Monospace, letterSpacing = 3.sp)
        }
        OutlinedButton(
            onClick = { vm.startDemo() }, enabled = !vm.pairBusy,
            modifier = Modifier.fillMaxWidth().height(52.dp), shape = PanelShape,
        ) { Text("TRY DEMO", fontFamily = FontFamily.Monospace, letterSpacing = 3.sp) }
        Text("Explore the app with sample data. No PC needed; nothing is sent anywhere.", color = Syra.Muted, fontSize = 13.sp)
    }
}

// ---------------- Dashboard ----------------
@Composable
fun DashboardTab(vm: CompanionViewModel) {
    val s = vm.status
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        if (vm.demo) {
            Surface(color = Color(0x334FD8FF), shape = PanelShape) {
                Text("Demo mode: sample data, nothing is sent to a PC. Exit from the ⋮ menu.", Modifier.padding(12.dp), color = Syra.Ink, fontSize = 13.sp)
            }
        }
        vm.connectionError?.let {
            Surface(color = Color(0x33FF3B3B), shape = PanelShape) { Text(it, Modifier.padding(12.dp), color = Syra.Ink, fontSize = 14.sp) }
        }
        if (s == null) {
            if (vm.connectionError == null) Box(Modifier.fillMaxWidth().padding(40.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            return@Column
        }
        Panel("STATUS") {
            Text(s.state.ifBlank { "ONLINE" }, color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 20.sp, fontWeight = FontWeight.Bold)
            Text(if (s.listening) "Microphone listening" else "Microphone off", color = Syra.Muted, fontSize = 13.sp)
        }
        Panel("SYSTEM") {
            Meter("CPU", s.cpu)
            Meter("RAM", s.ram, s.memory.takeIf { it.isNotBlank() && it != "Unavailable" })
            Meter("GPU", s.gpu)
        }
        Panel("TEMPERATURES") {
            Row(Modifier.fillMaxWidth()) {
                Reading("CPU", s.cpuTempF, Modifier.weight(1f))
                Reading("GPU", s.gpuTempF, Modifier.weight(1f))
            }
            if (s.fans.isNotEmpty()) {
                Spacer(Modifier.height(12.dp))
                s.fans.forEach { (name, rpm) ->
                    Row(Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
                        Text(name, color = Syra.Muted, fontSize = 13.sp, modifier = Modifier.weight(1f))
                        Text("$rpm RPM", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 13.sp)
                    }
                }
            }
            if (s.cpuTempF == null && s.gpuTempF == null && s.sensorStatus.isNotBlank()) {
                Spacer(Modifier.height(8.dp)); Text(s.sensorStatus, color = Syra.Muted, fontSize = 12.sp)
            }
        }
        Panel("SCREENS") {
            Text(
                when {
                    !s.screensEnabled -> "Turn on Screens & voice wake in Syra Setup to control displays from here."
                    !s.screensActive -> "Syra needs to be listening (LISTEN) to control screens."
                    s.screensOff -> "Displays are off."
                    else -> "Displays are on."
                },
                color = Syra.Muted, fontSize = 13.sp,
            )
            Spacer(Modifier.height(10.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(onClick = { vm.screens("wake") }, enabled = s.screensActive, shape = PanelShape, modifier = Modifier.weight(1f)) { Text("WAKE", fontFamily = FontFamily.Monospace) }
                OutlinedButton(onClick = { vm.screens("off") }, enabled = s.screensActive, shape = PanelShape, modifier = Modifier.weight(1f)) { Text("OFF", fontFamily = FontFamily.Monospace) }
            }
        }
    }
}

@Composable
private fun Meter(label: String, value: Double?, detail: String? = null) {
    Column(Modifier.padding(vertical = 6.dp)) {
        Row(Modifier.fillMaxWidth()) {
            Text(label, color = Syra.Muted, fontFamily = FontFamily.Monospace, fontSize = 13.sp, modifier = Modifier.weight(1f))
            Text(value?.let { "${it.toInt()}%" } ?: "—", color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 15.sp)
        }
        Spacer(Modifier.height(4.dp))
        LinearProgressIndicator(
            progress = { ((value ?: 0.0) / 100.0).toFloat().coerceIn(0f, 1f) },
            modifier = Modifier.fillMaxWidth().height(6.dp).clip(CircleShape),
            color = if ((value ?: 0.0) >= 90) Syra.Core else Syra.Cyan,
            trackColor = Syra.PanelHigh,
            drawStopIndicator = {},
        )
        detail?.let { Text(it, color = Syra.Muted, fontSize = 11.sp, modifier = Modifier.padding(top = 2.dp)) }
    }
}

@Composable
private fun Reading(label: String, tempF: Double?, modifier: Modifier) {
    Column(modifier) {
        Text(label, color = Syra.Muted, fontFamily = FontFamily.Monospace, fontSize = 13.sp)
        Text(
            tempF?.let { String.format(Locale.US, "%.0f°F", it) } ?: "—",
            color = when { tempF == null -> Syra.Muted; tempF >= 185 -> Syra.Core; tempF >= 160 -> Syra.Warn; else -> Syra.Ink },
            fontFamily = FontFamily.Monospace, fontSize = 30.sp,
        )
    }
}

// ---------------- Apps ----------------
@Composable
fun AppsTab(vm: CompanionViewModel) {
    var filter by remember { mutableStateOf("") }
    val shown = vm.apps.filter { it.contains(filter.trim(), ignoreCase = true) }
    Column(Modifier.fillMaxSize().padding(horizontal = 16.dp)) {
        OutlinedTextField(
            value = filter, onValueChange = { filter = it },
            leadingIcon = { Icon(Icons.Filled.Search, null) },
            placeholder = { Text("Search your Syra apps") },
            singleLine = true, modifier = Modifier.fillMaxWidth().padding(vertical = 12.dp),
        )
        when {
            vm.appsLoading && vm.apps.isEmpty() -> Box(Modifier.fillMaxWidth().padding(40.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            vm.apps.isEmpty() -> Text("No apps yet. Add mappings in Syra Setup → Applications & mappings, then pull them in with Refresh apps.", color = Syra.Muted)
            else -> LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(shown, key = { it }) { name ->
                    Surface(
                        shape = PanelShape, color = Syra.Panel, border = BorderStroke(1.dp, Color(0xFF14394A)),
                        modifier = Modifier.fillMaxWidth().clickable { vm.launch(name) },
                    ) {
                        Row(Modifier.padding(horizontal = 16.dp, vertical = 14.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text(name.replaceFirstChar { it.titlecase(Locale.US) }, color = Syra.Ink, fontSize = 16.sp, modifier = Modifier.weight(1f))
                            Text("OPEN", color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 12.sp)
                        }
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
    Column(
        Modifier.fillMaxSize().padding(16.dp).imePadding(),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            OutlinedButton(onClick = { vm.readAgenda() }, enabled = !vm.asking, shape = PanelShape, modifier = Modifier.weight(1f)) { Text("AGENDA", fontFamily = FontFamily.Monospace) }
            OutlinedButton(onClick = { vm.readCommute() }, enabled = !vm.asking, shape = PanelShape, modifier = Modifier.weight(1f)) { Text("COMMUTE", fontFamily = FontFamily.Monospace) }
        }
        Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState())) {
            if (vm.asking || vm.answer != null || vm.agenda.isNotEmpty()) {
                Panel(vm.answerTitle.uppercase().take(60)) {
                    if (vm.asking) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                            Spacer(Modifier.width(10.dp)); Text("Working on it…", color = Syra.Muted)
                        }
                    }
                    vm.agenda.forEach { item ->
                        Row(Modifier.fillMaxWidth().padding(vertical = 5.dp)) {
                            Text(item.time, color = Syra.Cyan, fontFamily = FontFamily.Monospace, fontSize = 13.sp, modifier = Modifier.width(86.dp))
                            Column(Modifier.weight(1f)) {
                                Text(item.title, color = Syra.Ink, fontSize = 15.sp)
                                if (item.location.isNotBlank()) Text(item.location, color = Syra.Muted, fontSize = 12.sp)
                            }
                        }
                    }
                    vm.answer?.takeIf { it.isNotBlank() }?.let { Text(it, color = Syra.Ink, fontSize = 15.sp, lineHeight = 22.sp) }
                }
            } else {
                val ready = vm.status?.chatReady != false
                Text(
                    if (ready) "Ask Syra anything. Answers come from the ChatGPT account connected on your PC."
                    else "Connect ChatGPT in Syra Setup on your PC to ask questions. Agenda and commute still work.",
                    color = Syra.Muted, fontSize = 14.sp, textAlign = TextAlign.Start, modifier = Modifier.padding(top = 8.dp),
                )
            }
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("Also speak the answer on the PC", color = Syra.Muted, fontSize = 13.sp, modifier = Modifier.weight(1f))
            Switch(checked = vm.speakOnPc, onCheckedChange = { vm.speakOnPc = it })
        }
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedTextField(
                value = vm.question, onValueChange = { vm.question = it },
                placeholder = { Text("Ask Syra…") }, modifier = Modifier.weight(1f), maxLines = 4,
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Send),
                keyboardActions = KeyboardActions(onSend = { vm.ask() }),
            )
            FilledIconButton(
                onClick = {
                    val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                        .putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                        .putExtra(RecognizerIntent.EXTRA_PROMPT, "Ask Syra")
                    try { speech.launch(intent) } catch (e: ActivityNotFoundException) { vm.toast = "No speech recognizer on this phone. Install or enable Google voice typing." }
                },
                enabled = !vm.asking,
                colors = IconButtonDefaults.filledIconButtonColors(containerColor = Syra.Core, contentColor = Color.White),
                modifier = Modifier.size(52.dp),
            ) { Icon(MicIcon, "Speak a question") }
            FilledIconButton(onClick = { vm.ask() }, enabled = !vm.asking && vm.question.isNotBlank(), modifier = Modifier.size(52.dp)) {
                Icon(Icons.AutoMirrored.Filled.Send, "Send")
            }
        }
    }
}
