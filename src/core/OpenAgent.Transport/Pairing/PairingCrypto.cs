using System.Security.Cryptography;
using System.Text;

namespace OpenAgent.Transport.Pairing;

/// <summary>
/// The v2 pairing math (docs/protocol.md §4): one P-256 identity key per peer,
/// a raw ECDH agreement zero-padded to 32 bytes, HKDF-SHA256 down to the
/// AES-256-GCM key, and the PIN/MAC pair that makes a man-in-the-middle visible
/// to the human on both screens. Every domain string here is mirrored in the
/// Android client's PairingCrypto — changing one without the other silently
/// breaks pairing.
/// </summary>
public static class PairingCrypto
{
    private const string HkdfSalt = "openagent-pair-v2";
    private const string HkdfInfo = "openagent/aes-256-gcm";
    private const string PinDomain = "openagent-pair-pin";
    private const string MacDomain = "openagent-pair-confirm";

    /// <summary>Generates an identity: (PKCS#8 private, SPKI public).</summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateIdentity()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return (ecdh.ExportPkcs8PrivateKey().ToArray(), ecdh.ExportSubjectPublicKeyInfo().ToArray());
    }

    /// <summary>The 32-byte AES-256-GCM pairing key both sides derive independently.</summary>
    public static byte[] DeriveSharedKey(byte[] privateKey, byte[] peerPublicKey)
    {
        using var ecdh = ECDiffieHellman.Create();
        ecdh.ImportPkcs8PrivateKey(privateKey, out _);
        using var peer = ECDiffieHellman.Create();
        peer.ImportSubjectPublicKeyInfo(peerPublicKey, out _);

        var z = ZeroPad(ecdh.DeriveRawSecretAgreement(peer.PublicKey), 32);
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            z,
            32,
            Encoding.UTF8.GetBytes(HkdfSalt),
            Encoding.UTF8.GetBytes(HkdfInfo));
    }

    /// <summary>
    /// The 6-digit code the host displays and the phone expects — derived from
    /// the shared key, so a man-in-the-middle's relaid keys cannot reproduce it.
    /// </summary>
    public static string PinFor(byte[] key, string hostId, string phoneId)
    {
        var digest = SHA256.HashData(Concat(
            PinDomain,
            key,
            Encoding.UTF8.GetBytes(hostId),
            Encoding.UTF8.GetBytes(phoneId)));
        var value = (digest[0] << 16) | (digest[1] << 8) | digest[2];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The proof the phone sends back in pair_confirm.</summary>
    public static byte[] ConfirmMac(byte[] key, string senderId, string receiverId) =>
        HMACSHA256.HashData(key, Concat(MacDomain, Encoding.UTF8.GetBytes(senderId), Encoding.UTF8.GetBytes(receiverId)));

    private static byte[] ZeroPad(byte[] value, int length)
    {
        if (value.Length >= length)
        {
            return value;
        }

        var padded = new byte[length];
        value.CopyTo(padded, length - value.Length);
        return padded;
    }

    private static byte[] Concat(string text, params byte[][] parts)
    {
        var head = Encoding.UTF8.GetBytes(text);
        var total = head.Length + parts.Sum(part => part.Length);
        var buffer = new byte[total];
        var offset = head.Length;
        Buffer.BlockCopy(head, 0, buffer, 0, head.Length);
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, buffer, offset, part.Length);
            offset += part.Length;
        }

        return buffer;
    }
}
