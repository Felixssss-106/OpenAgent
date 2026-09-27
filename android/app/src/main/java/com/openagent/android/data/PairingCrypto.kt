package com.openagent.android.data

import java.security.KeyFactory
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.PublicKey
import java.security.SecureRandom
import java.security.spec.PKCS8EncodedKeySpec
import java.security.spec.X509EncodedKeySpec
import javax.crypto.Cipher
import javax.crypto.KeyAgreement
import javax.crypto.Mac
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * The v2 pairing math (docs/protocol.md §4) — the Android mirror of the .NET
 * `PairingCrypto`. Every domain string here is byte-for-byte the same as the
 * C# side; changing one without the other silently breaks pairing.
 */
object PairingCrypto {
    private const val HKDF_SALT = "openagent-pair-v2"
    private const val HKDF_INFO = "openagent/aes-256-gcm"
    private const val PIN_DOMAIN = "openagent-pair-pin"
    private const val MAC_DOMAIN = "openagent-pair-confirm"
    private const val GCM_TAG_BITS = 128

    private val random = SecureRandom()

    /** Generates an identity; encoded as (PKCS#8 private, X.509 SPKI public) base64. */
    fun generateIdentity(): Pair<String, String> {
        val generator = java.security.KeyPairGenerator.getInstance("EC")
        generator.initialize(java.security.spec.ECGenParameterSpec("secp256r1"))
        val pair = generator.generateKeyPair()
        return Base64.encode(pair.private.encoded) to Base64.encode(pair.public.encoded)
    }

    fun publicKeyFromSpki(spki: String): PublicKey =
        KeyFactory.getInstance("EC").generatePublic(X509EncodedKeySpec(Base64.decode(spki)))

    fun privateKeyFromPkcs8(pkcs8: String): PrivateKey =
        KeyFactory.getInstance("EC").generatePrivate(PKCS8EncodedKeySpec(Base64.decode(pkcs8)))

    /** The 32-byte AES-256-GCM pairing key both sides derive independently. */
    fun deriveSharedKey(privatePkcs8: String, peerSpki: String): ByteArray {
        val agreement = KeyAgreement.getInstance("ECDH")
        agreement.init(privateKeyFromPkcs8(privatePkcs8))
        agreement.doPhase(publicKeyFromSpki(peerSpki), true)
        return hkdfSha256(zeroPad(agreement.generateSecret(), 32), HKDF_SALT.toByteArray(), HKDF_INFO.toByteArray(), 32)
    }

    /**
     * The 6-digit code the host displays and this phone expects — derived from
     * the shared key, so a man-in-the-middle's relaid keys cannot reproduce it.
     */
    fun pinFor(key: ByteArray, hostId: String, phoneId: String): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(
            PIN_DOMAIN.toByteArray() + key + hostId.toByteArray() + phoneId.toByteArray(),
        )
        val value = ((digest[0].toInt() and 0xFF) shl 16) or
            ((digest[1].toInt() and 0xFF) shl 8) or
            (digest[2].toInt() and 0xFF)
        return (value % 1_000_000).toString().padStart(6, '0')
    }

    /** The proof this phone sends back in pair_confirm. */
    fun confirmMac(key: ByteArray, senderId: String, receiverId: String): ByteArray {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(key, "HmacSHA256"))
        return mac.doFinal(MAC_DOMAIN.toByteArray() + senderId.toByteArray() + receiverId.toByteArray())
    }

    fun seal(key: ByteArray, plain: String): Pair<String, String> {
        val nonce = ByteArray(12).also(random::nextBytes)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(GCM_TAG_BITS, nonce))
        val sealed = cipher.doFinal(plain.toByteArray())
        return Base64.encode(nonce) to Base64.encode(sealed)
    }

    fun tryOpen(key: ByteArray, nonceB64: String, cipherB64: String): String? {
        val nonce = Base64.decode(nonceB64) ?: return null
        val sealed = Base64.decode(cipherB64) ?: return null
        if (nonce.size != 12 || sealed.size <= 16) return null
        return runCatching {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(GCM_TAG_BITS, nonce))
            String(cipher.doFinal(sealed), Charsets.UTF_8)
        }.getOrNull()
    }

    /** HKDF-SHA256 (RFC 5869): extract with [salt], then one expand block. */
    private fun hkdfSha256(ikm: ByteArray, salt: ByteArray, info: ByteArray, length: Int): ByteArray {
        val extract = Mac.getInstance("HmacSHA256")
        extract.init(SecretKeySpec(if (salt.isEmpty()) ByteArray(32) else salt, "HmacSHA256"))
        val prk = extract.doFinal(ikm)

        val expand = Mac.getInstance("HmacSHA256")
        expand.init(SecretKeySpec(prk, "HmacSHA256"))
        return expand.doFinal(info + 1)
    }

    /** The raw ECDH secret drops leading zeros on some providers; both sides pad. */
    private fun zeroPad(secret: ByteArray, length: Int): ByteArray =
        if (secret.size >= length) secret else ByteArray(length - secret.size) + secret
}

/** Base64 in the flavour .NET's Convert.ToBase64String speaks (with padding). */
object Base64 {
    fun encode(bytes: ByteArray): String = android.util.Base64.encodeToString(bytes, android.util.Base64.NO_WRAP)

    fun decode(text: String): ByteArray? =
        runCatching { android.util.Base64.decode(text, android.util.Base64.NO_WRAP) }.getOrNull()
}
