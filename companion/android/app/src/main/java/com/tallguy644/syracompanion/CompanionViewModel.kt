package com.tallguy644.syracompanion

import android.app.Application
import android.os.Build
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

data class AgendaEntry(val time: String, val title: String, val location: String)

class CompanionViewModel(app: Application) : AndroidViewModel(app) {
    private val store = PairingStore(app)
    private val themePrefs = app.getSharedPreferences("theme", android.content.Context.MODE_PRIVATE)
    /** Ignore the PC's colors briefly after a change on the phone, so a poll can't undo it mid-flight. */
    private var themeHoldUntil = 0L

    init {
        SyraColors.of(themePrefs.getString("accent", null), themePrefs.getString("rings", null), themePrefs.getString("core", null))?.let { Syra.apply(it) }
    }

    var pairing by mutableStateOf(store.load())
        private set

    // Pairing form
    var pairHost by mutableStateOf(store.lastHost())
    var pairPort by mutableStateOf(store.lastPort().toString())
    var pairCode by mutableStateOf("")
    var pairBusy by mutableStateOf(false)
        private set
    var pairError by mutableStateOf<String?>(null)
        private set

    // Live PC state
    var status by mutableStateOf<PcStatus?>(null)
        private set
    var connectionError by mutableStateOf<String?>(null)
        private set
    val apps = mutableStateListOf<String>()
    /** Recent samples (about two minutes at one every three seconds) for the live graphs. */
    val cpuHistory = mutableStateListOf<Float>()
    val gpuHistory = mutableStateListOf<Float>()
    var appsLoading by mutableStateOf(false)
        private set

    // Ask
    var question by mutableStateOf("")
    var speakOnPc by mutableStateOf(false)
    var answer by mutableStateOf<String?>(null)
        private set
    var answerTitle by mutableStateOf("")
        private set
    val agenda = mutableStateListOf<AgendaEntry>()
    var asking by mutableStateOf(false)
        private set

    /** One-line feedback shown as a snackbar. */
    var toast by mutableStateOf<String?>(null)

    private var poller: Job? = null

    /** True while exploring the app with sample data instead of a paired PC. */
    var demo by mutableStateOf(false)
        private set
    private val demoBackend = DemoBackend()

    private fun api(): SyraBackend? = if (demo) demoBackend else pairing?.let { SyraApi(it.host, it.port, it.token) }

    fun startDemo() {
        demo = true; pairError = null
        pairing = Pairing("demo", 0, "", "Syra")
        apps.clear(); answer = null; agenda.clear()
        cpuHistory.clear(); gpuHistory.clear()
        for (i in 0 until HISTORY) {
            cpuHistory.add((24 + 14 * kotlin.math.sin(i / 4.0) + (i * 7 % 5)).toFloat())
            gpuHistory.add((18 + 12 * kotlin.math.sin(i / 4.0 + 1.3) + (i * 3 % 4)).toFloat())
        }
    }

    init {
        // For now the app opens in demo mode until a PC is paired. Exit demo from the menu to pair.
        if (START_IN_DEMO && pairing == null) startDemo()
    }

    companion object { const val START_IN_DEMO = true; const val HISTORY = 40 }

    private fun resetSession() {
        pairing = null; status = null; connectionError = null; apps.clear(); answer = null; agenda.clear(); question = ""
        cpuHistory.clear(); gpuHistory.clear()
    }

    // ---------- Pairing ----------
    fun pair() {
        val host = pairHost.trim()
        val port = pairPort.trim().toIntOrNull()
        val code = pairCode.filter { it.isDigit() }
        when {
            host.isEmpty() -> { pairError = "Enter your PC's address."; return }
            port == null || port !in 1024..65535 -> { pairError = "Port should be a number like 47820."; return }
            code.length != 6 -> { pairError = "Enter the 6-digit code shown in Syra Setup."; return }
        }
        pairBusy = true; pairError = null
        viewModelScope.launch {
            try {
                val probe = SyraApi(host, port!!, null)
                probe.hello()
                val result = probe.pair(code, "${Build.MANUFACTURER.replaceFirstChar { it.uppercase() }} ${Build.MODEL}")
                val p = Pairing(host, port, result.getString("token"), result.optString("name", "Syra"))
                store.save(p)
                pairing = p; pairCode = ""
                refreshApps()
            } catch (e: ApiException) {
                pairError = e.message
            } catch (e: Exception) {
                pairError = "Pairing failed: ${e.message}"
            } finally {
                pairBusy = false
            }
        }
    }

    fun unpair() {
        if (demo) { stopPolling(); demo = false; resetSession(); return }
        val api = api()
        viewModelScope.launch {
            try { api?.unpair() } catch (e: Exception) { /* Forget locally even if the PC is offline. */ }
            store.clear(); resetSession()
        }
    }

    private fun handle(e: Exception) {
        if (e is ApiException && e.status == 401) {
            store.clear(); pairing = null; status = null
            pairError = "This phone was removed from Syra. Pair it again."
        } else {
            toast = e.message ?: "Something went wrong."
        }
    }

    // ---------- Status polling (only while the app is on screen) ----------
    fun startPolling() {
        if (poller?.isActive == true) return
        poller = viewModelScope.launch {
            while (isActive) {
                val api = api() ?: break
                try {
                    val s = PcStatus.from(api.status())
                    syncThemeFrom(s.theme)
                    status = s; connectionError = null
                    record(cpuHistory, s.cpu); record(gpuHistory, s.gpu)
                } catch (e: ApiException) {
                    if (e.status == 401) { handle(e); break }
                    connectionError = e.message
                } catch (e: Exception) {
                    connectionError = e.message
                }
                delay(if (demo) 1_500 else 3_000)
            }
        }
        if (apps.isEmpty()) refreshApps()
    }

    private fun record(list: MutableList<Float>, value: Double?) {
        if (value == null) return
        list.add(value.toFloat()); while (list.size > HISTORY) list.removeAt(0)
    }

    fun stopPolling() { poller?.cancel(); poller = null }

    // ---------- Actions ----------
    fun refreshApps() {
        val api = api() ?: return
        appsLoading = true
        viewModelScope.launch {
            try { val list = api.apps(); apps.clear(); apps.addAll(list) } catch (e: Exception) { handle(e) } finally { appsLoading = false }
        }
    }

    fun launch(name: String) = runAction { it.launch(name) }
    fun screens(action: String) = runAction { it.screens(action) }

    // ---------- Colors (kept in sync with Syra on the PC) ----------
    private fun saveTheme(c: SyraColors) =
        themePrefs.edit().putString("accent", c.accent).putString("rings", c.rings).putString("core", c.core).apply()

    private fun syncThemeFrom(theme: SyraColors?) {
        if (theme == null || System.currentTimeMillis() < themeHoldUntil || theme == Syra.colors) return
        Syra.apply(theme); saveTheme(theme)
    }

    /** Shows colors on the phone only, while choosing. */
    fun previewColors(c: SyraColors) = Syra.apply(c)

    /** Applies colors on the phone and sends them to Syra so the PC changes too. */
    fun setColors(c: SyraColors) {
        Syra.apply(c); saveTheme(c)
        themeHoldUntil = System.currentTimeMillis() + 6_000
        val api = api() ?: return
        viewModelScope.launch {
            try { toast = api.setTheme(c).ifBlank { "Colors updated on your PC." } } catch (e: Exception) { handle(e) }
        }
    }

    /** Media keys on the PC; refreshes status right away so now-playing updates. */
    fun media(action: String) {
        val api = api() ?: return
        viewModelScope.launch {
            try {
                api.media(action)
                status = PcStatus.from(api.status())
            } catch (e: Exception) { handle(e) }
        }
    }

    private fun runAction(block: suspend (SyraBackend) -> String) {
        val api = api() ?: return
        viewModelScope.launch {
            try { toast = block(api).ifBlank { "Done." } } catch (e: Exception) { handle(e) }
        }
    }

    fun ask() {
        val q = question.trim()
        if (q.isEmpty() || asking) return
        val api = api() ?: return
        asking = true; answerTitle = q; answer = null; agenda.clear()
        viewModelScope.launch {
            try { answer = api.ask(q, speakOnPc); question = "" }
            catch (e: Exception) { answer = null; handle(e) }
            finally { asking = false }
        }
    }

    fun readAgenda() {
        val api = api() ?: return
        if (asking) return
        asking = true; answerTitle = "Today's agenda"; answer = null; agenda.clear()
        viewModelScope.launch {
            try {
                val json = api.agenda()
                val items = json.optJSONArray("items")
                if (items != null) for (i in 0 until items.length()) items.getJSONObject(i).let {
                    agenda.add(AgendaEntry(it.optString("time"), it.optString("title"), it.optString("location")))
                }
                answer = if (agenda.isEmpty()) json.optString("text") else if (json.optBoolean("offline")) "Showing your saved calendar." else ""
            } catch (e: Exception) { handle(e) } finally { asking = false }
        }
    }

    fun readCommute() {
        val api = api() ?: return
        if (asking) return
        asking = true; answerTitle = "Work commute"; answer = null; agenda.clear()
        viewModelScope.launch {
            try { answer = api.commute() } catch (e: Exception) { handle(e) } finally { asking = false }
        }
    }
}
