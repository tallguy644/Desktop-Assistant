package com.tallguy644.syracompanion

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.SocketTimeoutException
import java.net.URL

class ApiException(val status: Int, message: String) : Exception(message)

/** Talks to the companion API inside Syra on the PC. */
class SyraApi(host: String, private val port: Int, private val token: String?) : SyraBackend {
    private val host = host.trim().removePrefix("http://").removePrefix("https://").trimEnd('/')

    private fun url(path: String): URL {
        val h = if (host.contains(':') && !host.startsWith("[")) "[$host]" else host // IPv6 literal
        return URL("http://$h:$port$path")
    }

    suspend fun call(method: String, path: String, body: JSONObject? = null, timeoutMs: Int = 10_000): JSONObject =
        withContext(Dispatchers.IO) {
            val conn = url(path).openConnection() as HttpURLConnection
            try {
                conn.requestMethod = method
                conn.connectTimeout = 6_000
                conn.readTimeout = timeoutMs
                conn.useCaches = false
                conn.setRequestProperty("Accept", "application/json")
                if (token != null) conn.setRequestProperty("Authorization", "Bearer $token")
                if (body != null) {
                    val bytes = body.toString().toByteArray(Charsets.UTF_8)
                    conn.doOutput = true
                    conn.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                    conn.setFixedLengthStreamingMode(bytes.size)
                    conn.outputStream.use { it.write(bytes) }
                }
                val status = conn.responseCode
                val stream = if (status in 200..299) conn.inputStream else conn.errorStream
                val text = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
                val json = try { if (text.isBlank()) JSONObject() else JSONObject(text) } catch (e: JSONException) {
                    throw ApiException(status, "That address answered, but it isn't Syra.")
                }
                if (status !in 200..299) throw ApiException(status, json.optString("error", "Request failed ($status)."))
                json
            } catch (e: SocketTimeoutException) {
                throw ApiException(0, "Syra didn't answer in time. Is the PC awake and on your network or Tailscale?")
            } catch (e: IOException) {
                throw ApiException(0, "Can't reach Syra at $host:$port. Check the address, that the PC is on, and that Tailscale is connected.")
            } finally {
                conn.disconnect()
            }
        }

    suspend fun hello() = call("GET", "/api/hello").also {
        if (it.optString("app") != "syra") throw ApiException(0, "That address answered, but it isn't Syra.")
    }

    suspend fun pair(code: String, device: String) =
        call("POST", "/api/pair", JSONObject().put("code", code).put("device", device))

    override suspend fun status(): JSONObject = call("GET", "/api/status")
    override suspend fun apps(): List<String> = call("GET", "/api/apps").optJSONArray("apps").strings()
    override suspend fun launch(name: String): String = call("POST", "/api/apps/launch", JSONObject().put("name", name)).optString("message")
    override suspend fun screens(action: String): String = call("POST", "/api/screens", JSONObject().put("action", action)).optString("message")
    override suspend fun ask(question: String, speak: Boolean): String =
        call("POST", "/api/ask", JSONObject().put("question", question).put("speak", speak), timeoutMs = 180_000).optString("answer")
    override suspend fun agenda(): JSONObject = call("GET", "/api/agenda", timeoutMs = 25_000)
    override suspend fun commute(): String = call("GET", "/api/commute", timeoutMs = 40_000).optString("text")
    override suspend fun unpair(): JSONObject = call("POST", "/api/unpair")
}

fun JSONArray?.strings(): List<String> = if (this == null) emptyList() else List(length()) { optString(it) }

/** Plain-Kotlin view of /api/status. */
data class PcStatus(
    val name: String,
    val state: String,
    val listening: Boolean,
    val cpu: Double?,
    val ram: Double?,
    val gpu: Double?,
    val cpuTempF: Double?,
    val gpuTempF: Double?,
    val memory: String,
    val fans: List<Pair<String, Int>>,
    val sensorStatus: String,
    val screensEnabled: Boolean,
    val screensActive: Boolean,
    val screensOff: Boolean,
    val chatReady: Boolean,
    val calendar: Boolean,
    val commute: Boolean,
) {
    companion object {
        private fun JSONObject.num(key: String): Double? = if (isNull(key) || !has(key)) null else optDouble(key).takeIf { !it.isNaN() }

        fun from(json: JSONObject): PcStatus {
            val stats = json.optJSONObject("stats") ?: JSONObject()
            val screens = json.optJSONObject("screens") ?: JSONObject()
            val chat = json.optJSONObject("chat") ?: JSONObject()
            val fans = stats.optJSONArray("fans")?.let { arr ->
                List(arr.length()) { i -> arr.getJSONObject(i).let { it.optString("name") to it.optDouble("rpm").toInt() } }
            } ?: emptyList()
            return PcStatus(
                name = json.optString("name", "Syra"),
                state = json.optString("state"),
                listening = json.optBoolean("listening"),
                cpu = stats.num("cpu"), ram = stats.num("ram"), gpu = stats.num("gpu"),
                cpuTempF = stats.num("cpuTempF"), gpuTempF = stats.num("gpuTempF"),
                memory = stats.optString("memory"),
                fans = fans,
                sensorStatus = stats.optString("sensorStatus"),
                screensEnabled = screens.optBoolean("enabled"),
                screensActive = screens.optBoolean("active"),
                screensOff = screens.optBoolean("off"),
                chatReady = chat.optBoolean("ready"),
                calendar = json.optBoolean("calendar"),
                commute = json.optBoolean("commute"),
            )
        }
    }
}
