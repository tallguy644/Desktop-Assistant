package com.tallguy644.syracompanion

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

data class Pairing(val host: String, val port: Int, val token: String, val pcName: String)

/** Saves the pairing; the token is encrypted with a key that never leaves the Android Keystore. */
class PairingStore(context: Context) {
    private val prefs = context.getSharedPreferences("pairing", Context.MODE_PRIVATE)

    fun load(): Pairing? {
        val host = prefs.getString("host", null) ?: return null
        val sealed = prefs.getString("token", null) ?: return null
        val token = try { decrypt(sealed) } catch (e: Exception) { return null }
        return Pairing(host, prefs.getInt("port", DEFAULT_PORT), token, prefs.getString("name", "Syra") ?: "Syra")
    }

    fun save(p: Pairing) {
        prefs.edit()
            .putString("host", p.host)
            .putInt("port", p.port)
            .putString("name", p.pcName)
            .putString("token", encrypt(p.token))
            .apply()
    }

    fun clear() = prefs.edit().remove("token").remove("name").apply() // keep host/port to make re-pairing quick

    fun lastHost(): String = prefs.getString("host", "") ?: ""
    fun lastPort(): Int = prefs.getInt("port", DEFAULT_PORT)

    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(ALIAS, null) as? SecretKey)?.let { return it }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build()
        )
        return generator.generateKey()
    }

    private fun encrypt(plain: String): String {
        val cipher = Cipher.getInstance(TRANSFORM).apply { init(Cipher.ENCRYPT_MODE, key()) }
        val out = cipher.iv + cipher.doFinal(plain.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(out, Base64.NO_WRAP)
    }

    private fun decrypt(sealed: String): String {
        val data = Base64.decode(sealed, Base64.NO_WRAP)
        val cipher = Cipher.getInstance(TRANSFORM).apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, data, 0, 12)) }
        return String(cipher.doFinal(data, 12, data.size - 12), Charsets.UTF_8)
    }

    companion object {
        const val DEFAULT_PORT = 47820
        private const val ALIAS = "syra-companion-token"
        private const val TRANSFORM = "AES/GCM/NoPadding"
    }
}
