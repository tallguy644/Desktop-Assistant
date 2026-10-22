package com.tallguy644.syracompanion

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties

private val Swatches = listOf(
    "#00E6EB", "#4FD8FF", "#A5D7FF", "#2F7BFF", "#5AFFA0", "#B9FFD3", "#C79BFF", "#BFAAFF",
    "#FF64AD", "#F22330", "#FF6859", "#FFB347", "#FFC25C", "#FFE0A1", "#FFFFFF",
)

private enum class Role(val label: String) { Accent("HUD ACCENT / TEXT AND CIRCUITS"), Rings("REACTOR RINGS"), Core("REACTOR CORE") }

/**
 * Pick Syra's three colors. Changes preview live on the phone; APPLY saves them on the phone and sends them to Syra on the PC.
 * Cancel restores what you had.
 */
@Composable
fun ColorEditor(vm: CompanionViewModel, onClose: () -> Unit) {
    val original = remember { Syra.colors }
    var draft by remember { mutableStateOf(original) }
    fun update(c: SyraColors) { draft = c; vm.previewColors(c) }
    fun cancel() { vm.previewColors(original); onClose() }

    Dialog(onDismissRequest = { cancel() }, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        HudBackground {
            Column(
                Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(20.dp).imePadding(),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                GlitchText("COLOR MATRIX", fontSize = 20.sp, letterSpacing = 5.sp, color = Syra.Cyan)
                HudLabel(if (vm.demo) "DEMO: CHANGES STAY ON THIS PHONE" else "SYNCS WITH SYRA ON YOUR PC", color = Syra.Muted)
                Reactor(150.dp, active = true)
                Panel("PRESETS") {
                    Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        SyraColors.Presets.forEach { (name, c) ->
                            Column(
                                Modifier.hudFrame(c.accentColor, fill = c.accentColor.copy(alpha = if (c == draft) 0.25f else 0.08f), cut = 8.dp)
                                    .clickable { update(c) }.padding(horizontal = 14.dp, vertical = 10.dp),
                                horizontalAlignment = Alignment.CenterHorizontally,
                            ) {
                                Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                                    listOf(c.accentColor, c.ringsColor, c.coreColor).forEach { Dot(it, 12.dp) }
                                }
                                Spacer(Modifier.height(6.dp))
                                Text(name, color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 11.sp, letterSpacing = 1.5.sp)
                            }
                        }
                    }
                }
                Role.entries.forEach { role ->
                    val value = when (role) { Role.Accent -> draft.accent; Role.Rings -> draft.rings; Role.Core -> draft.core }
                    fun set(hex: String) = update(when (role) { Role.Accent -> draft.copy(accent = hex); Role.Rings -> draft.copy(rings = hex); Role.Core -> draft.copy(core = hex) })
                    Panel(role.label, accent = SyraColors.parse(value)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Dot(SyraColors.parse(value), 34.dp)
                            Spacer(Modifier.width(14.dp))
                            HexField(value) { set(it) }
                        }
                        Spacer(Modifier.height(12.dp))
                        Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Swatches.forEach { hex ->
                                val selected = hex.equals(value, ignoreCase = true)
                                Box(
                                    Modifier.size(34.dp).clip(CircleShape)
                                        .border(if (selected) 3.dp else 1.dp, if (selected) Color.White else Color.White.copy(alpha = 0.2f), CircleShape)
                                        .clickable { set(hex) },
                                ) { Dot(SyraColors.parse(hex), 34.dp) }
                            }
                        }
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    HudButton("CANCEL", { cancel() }, Modifier.weight(1f), primary = false, accent = Syra.Muted)
                    HudButton(if (vm.demo) "APPLY" else "APPLY + SYNC", { vm.setColors(draft); onClose() }, Modifier.weight(1.4f))
                }
                Spacer(Modifier.height(12.dp))
            }
        }
    }
}

@Composable
private fun Dot(color: Color, size: androidx.compose.ui.unit.Dp) {
    Canvas(Modifier.size(size)) {
        drawCircle(color.copy(alpha = 0.35f), this.size.minDimension / 2)
        drawCircle(color, this.size.minDimension / 2 * 0.78f)
    }
}

/** #RRGGBB entry; only reports complete, valid colors. */
@Composable
private fun HexField(value: String, onValid: (String) -> Unit) {
    var text by remember(value) { mutableStateOf(value) }
    val valid = SyraColors.clean(text) != null
    Box(Modifier.width(150.dp).hudFrame(if (valid) Syra.Teal else Syra.Core, fill = Color(0xCC07131F), cut = 6.dp).padding(horizontal = 12.dp, vertical = 10.dp)) {
        BasicTextField(
            value = text,
            onValueChange = { v ->
                text = v.take(7).uppercase()
                SyraColors.clean(text)?.let(onValid)
            },
            singleLine = true,
            textStyle = TextStyle(color = Syra.Ink, fontFamily = FontFamily.Monospace, fontSize = 17.sp, letterSpacing = 2.sp),
            cursorBrush = SolidColor(Syra.Cyan),
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Characters, imeAction = ImeAction.Done),
        )
    }
}
