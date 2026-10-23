package com.tallguy644.syracompanion

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.foundation.layout.Column
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver

class MainActivity : ComponentActivity() {
    private val vm: CompanionViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            SyraTheme {
                var booted by rememberSaveable { mutableStateOf(false) }
                when {
                    !booted -> BootSequence { booted = true }
                    vm.pairing == null -> PairScreen(vm)
                    else -> MainScreen(vm)
                }
            }
        }
    }
}

private enum class Tab(val label: String, val icon: ImageVector) {
    Dashboard("PC", ReactorIcon),
    Media("Media", MediaIcon),
    Apps("Apps", Icons.AutoMirrored.Filled.List),
    Ask("Ask", MicIcon),
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MainScreen(vm: CompanionViewModel) {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var menu by remember { mutableStateOf(false) }
    var colors by remember { mutableStateOf(false) }
    val snackbar = remember { SnackbarHostState() }

    // Poll the PC only while the app is visible.
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_START -> vm.startPolling()
                Lifecycle.Event.ON_STOP -> vm.stopPolling()
                else -> Unit
            }
        }
        owner.lifecycle.addObserver(observer)
        onDispose { owner.lifecycle.removeObserver(observer); vm.stopPolling() }
    }
    if (colors) ColorEditor(vm) { colors = false }
    LaunchedEffect(vm.toast) {
        vm.toast?.let { snackbar.showSnackbar(it); vm.toast = null }
    }

    HudBackground {
    Scaffold(
        containerColor = Color.Transparent,
        snackbarHost = { SnackbarHost(snackbar) },
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Color.Transparent, titleContentColor = Syra.Cyan, actionIconContentColor = Syra.Cyan),
                title = {
                    Column {
                        GlitchText((vm.pairing?.pcName ?: "Syra").uppercase() + " // " + Tab.entries[tab].label.uppercase(), fontSize = 18.sp, letterSpacing = 3.sp, color = Syra.Cyan)
                        HudLabel(if (vm.demo) "DEMO LINK · SIMULATED" else if (vm.connectionError == null && vm.status != null) "UPLINK ACTIVE" else "UPLINK SEARCHING", color = if (vm.demo) Syra.Warn else Syra.Muted)
                    }
                },
                actions = {
                    StatusDot(vm.status != null && vm.connectionError == null)
                    Box {
                        IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "Menu") }
                        DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                            DropdownMenuItem(text = { Text("Colors") }, onClick = { menu = false; colors = true })
                            DropdownMenuItem(text = { Text("Refresh apps") }, onClick = { menu = false; vm.refreshApps() })
                            DropdownMenuItem(text = { Text(if (vm.demo) "Connect to my PC" else "Unpair this phone") }, onClick = { menu = false; vm.unpair() })
                        }
                    }
                },
            )
        },
        bottomBar = {
            HudNavBar(Tab.entries.map { it.label }, Tab.entries.map { it.icon }, tab) { tab = it }
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when (Tab.entries[tab]) {
                Tab.Dashboard -> DashboardTab(vm)
                Tab.Media -> MediaTab(vm)
                Tab.Apps -> AppsTab(vm)
                Tab.Ask -> AskTab(vm)
            }
        }
    }
    }
}
