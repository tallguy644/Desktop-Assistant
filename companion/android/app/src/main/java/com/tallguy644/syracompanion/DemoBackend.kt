package com.tallguy644.syracompanion

import kotlinx.coroutines.delay
import org.json.JSONArray
import org.json.JSONObject
import kotlin.math.sin
import kotlin.random.Random

/** What the app needs from Syra. The real PC ([SyraApi]) and the demo both provide it. */
interface SyraBackend {
    suspend fun status(): JSONObject
    suspend fun apps(): List<String>
    suspend fun launch(name: String): String
    suspend fun screens(action: String): String
    suspend fun ask(question: String, speak: Boolean): String
    suspend fun agenda(): JSONObject
    suspend fun commute(): String
    suspend fun unpair(): JSONObject
}

/** Sample data so the app can be explored without a PC. Nothing leaves the phone. */
class DemoBackend : SyraBackend {
    private var tick = 0
    private var screensOff = false
    private val appList = listOf("chrome", "spotify", "discord", "steam", "notepad", "calculator", "visual studio code", "obs studio")

    override suspend fun status(): JSONObject {
        tick++
        val wave = sin(tick / 4.0)
        fun jitter(base: Double, spread: Double) = (base + wave * spread + Random.nextDouble(-spread / 3, spread / 3)).coerceIn(0.0, 100.0)
        val stats = JSONObject()
            .put("cpu", jitter(24.0, 14.0))
            .put("ram", jitter(47.0, 3.0))
            .put("gpu", jitter(18.0, 12.0))
            .put("cpuTempF", 128.0 + wave * 9 + Random.nextDouble(-1.5, 1.5))
            .put("gpuTempF", 115.0 + wave * 7 + Random.nextDouble(-1.5, 1.5))
            .put("memory", "15.1 GB of 32.0 GB in use")
            .put("sensorStatus", "Sensor feed connected (demo)")
            .put("fans", JSONArray()
                .put(JSONObject().put("name", "CPU Fan").put("rpm", 1150 + (wave * 120).toInt()))
                .put(JSONObject().put("name", "Pump").put("rpm", 2840))
                .put(JSONObject().put("name", "Case Fan #1").put("rpm", 860 + (wave * 60).toInt())))
        return JSONObject()
            .put("name", "Syra")
            .put("state", if (screensOff) "SCREENS OFF" else "LISTENING")
            .put("listening", true)
            .put("stats", stats)
            .put("screens", JSONObject().put("enabled", true).put("active", true).put("off", screensOff))
            .put("chat", JSONObject().put("ready", true).put("busy", false))
            .put("calendar", true)
            .put("commute", true)
    }

    override suspend fun apps(): List<String> { delay(300); return appList.sorted() }

    override suspend fun launch(name: String): String { delay(250); return "Demo: Syra would open ${name.replaceFirstChar { it.titlecase() }} on your PC." }

    override suspend fun screens(action: String): String {
        delay(250)
        screensOff = action == "off"
        return if (screensOff) "Demo: screens would turn off." else "Demo: screens would wake up."
    }

    override suspend fun ask(question: String, speak: Boolean): String {
        delay(1500)
        return "This is a demo answer. Once paired, your question goes to Syra on your PC, which asks the ChatGPT account connected there and sends the reply back here." +
            (if (speak) " Syra would also read it aloud on the PC." else "") +
            "\n\nYou asked: “$question”"
    }

    override suspend fun agenda(): JSONObject {
        delay(700)
        val items = JSONArray()
            .put(item("9:00 AM", "Team stand-up", "Video call"))
            .put(item("12:30 PM", "Lunch with Sam", "Downtown"))
            .put(item("3:00 PM", "Dentist", "Main St Dental"))
            .put(item("6:00 PM", "Work shift", "Warehouse"))
        return JSONObject().put("text", "Today's agenda (demo).").put("offline", false).put("items", items)
    }

    private fun item(time: String, title: String, location: String) =
        JSONObject().put("time", time).put("title", title).put("location", location).put("allDay", false)

    override suspend fun commute(): String {
        delay(900)
        return "Demo: Current driving time is 22 minutes. Leave at 5:28 PM to arrive 10 minutes before work. Traffic may change."
    }

    override suspend fun unpair(): JSONObject = JSONObject()
}
