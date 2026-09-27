using System.Security.Cryptography;

namespace OpenAgent.Transport.Pairing;

/// <summary>
/// AES-256-GCM sealing for application envelopes (docs/protocol.md §4.4): the
/// human payload moves from <c>text</c> into <c>cipher</c> (ciphertext followed
/// by the 16-byte tag) with a fresh 12-byte <c>nonce</c>. The layout matches the
/// Android client's Cipher "AES/GCM/NoPadding", which appends the tag the same
/// way.
/// </summary>
public static class EnvelopeCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Returns a copy of <paramref name="envelope"/> with its text encrypted.</summary>
    public static LanMessageEnvelope Seal(LanMessageEnvelope envelope, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = System.Text.Encoding.UTF8.GetBytes(envelope.Text);
        var cipher = new byte[plain.Length + TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher.AsSpan(0, plain.Length), cipher.AsSpan(plain.Length));

        return envelope with
        {
            Text = string.Empty,
            Nonce = Convert.ToBase64String(nonce),
            Cipher = Convert.ToBase64String(cipher),
        };
    }

    /// <summary>
    /// Opens a sealed envelope. False on a bad tag, a truncated payload, or a
    /// datagram that was never sealed — the caller drops it silently.
    /// </summary>
    public static bool TryOpen(LanMessageEnvelope envelope, byte[] key, out LanMessageEnvelope opened)
    {
        opened = envelope;
        if (string.IsNullOrEmpty(envelope.Nonce) || string.IsNullOrEmpty(envelope.Cipher))
        {
            return false;
        }

        byte[] nonce;
        byte[] cipher;
        try
        {
            nonce = Convert.FromBase64String(envelope.Nonce);
            cipher = Convert.FromBase64String(envelope.Cipher);
        }
        catch (FormatException)
        {
            return false;
        }

        if (nonce.Length != NonceSize || cipher.Length < TagSize)
        {
            return false;
        }

        var plain = new byte[cipher.Length - TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher.AsSpan(0, plain.Length), cipher.AsSpan(plain.Length), plain);
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }

        opened = envelope with { Text = System.Text.Encoding.UTF8.GetString(plain), Cipher = null, Nonce = null };
        return true;
    }
}
