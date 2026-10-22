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
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver

class MainActivity : ComponentActivity() {
    private val vm: CompanionViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            SyraTheme {
                if (vm.pairing == null) PairScreen(vm) else MainScreen(vm)
            }
        }
    }
}

private enum class Tab(val label: String, val icon: ImageVector) {
    Dashboard("PC", ReactorIcon),
    Apps("Apps", Icons.AutoMirrored.Filled.List),
    Ask("Ask", MicIcon),
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MainScreen(vm: CompanionViewModel) {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var menu by remember { mutableStateOf(false) }
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
    LaunchedEffect(vm.toast) {
        vm.toast?.let { snackbar.showSnackbar(it); vm.toast = null }
    }

    Scaffold(
        containerColor = Syra.Background,
        snackbarHost = { SnackbarHost(snackbar) },
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Syra.Background, titleContentColor = Syra.Cyan),
                title = {
                    Text((vm.pairing?.pcName ?: "Syra").uppercase() + " / " + Tab.entries[tab].label.uppercase() + (if (vm.demo) "  · DEMO" else ""), fontFamily = FontFamily.Monospace)
                },
                actions = {
                    StatusDot(vm.status != null && vm.connectionError == null)
                    Box {
                        IconButton(onClick = { menu = true }) { Icon(Icons.Filled.MoreVert, "Menu") }
                        DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                            DropdownMenuItem(text = { Text("Refresh apps") }, onClick = { menu = false; vm.refreshApps() })
                            DropdownMenuItem(text = { Text(if (vm.demo) "Exit demo" else "Unpair this phone") }, onClick = { menu = false; vm.unpair() })
                        }
                    }
                },
            )
        },
        bottomBar = {
            NavigationBar(containerColor = Syra.Panel) {
                Tab.entries.forEachIndexed { i, t ->
                    NavigationBarItem(
                        selected = tab == i,
                        onClick = { tab = i },
                        icon = { Icon(t.icon, t.label) },
                        label = { Text(t.label) },
                        colors = NavigationBarItemDefaults.colors(
                            selectedIconColor = Syra.Background,
                            indicatorColor = Syra.Cyan,
                            unselectedIconColor = Syra.Muted,
                            unselectedTextColor = Syra.Muted,
                            selectedTextColor = Syra.Cyan,
                        ),
                    )
                }
            }
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when (Tab.entries[tab]) {
                Tab.Dashboard -> DashboardTab(vm)
                Tab.Apps -> AppsTab(vm)
                Tab.Ask -> AskTab(vm)
            }
        }
    }
}
