using OpenAgent.Transport;
using OpenAgent.Transport.Pairing;
using Xunit;

namespace OpenAgent.Transport.Tests;

/// <summary>
/// The v2 handshake (docs/protocol.md §4) played out between a simulated phone
/// and the host's PairingService — plus the envelope sealing both sides use
/// once paired.
/// </summary>
public sealed class PairingServiceTests
{
    private const string HostId = "lan:HOST-1";
    private const string PhoneId = "android:Pixel-9";

    private sealed class CapturingTransport : ITransport
    {
        public List<(string Target, LanMessageEnvelope Envelope)> Sent { get; } = new();

        public Task<IReadOnlyList<DeviceRecord>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceRecord>>(Array.Empty<DeviceRecord>());

        public Task SendAsync(string targetDeviceId, byte[] payload, CancellationToken cancellationToken = default)
        {
            Sent.Add((targetDeviceId, LanMessageEnvelope.Decode(payload) ?? throw new InvalidOperationException("bad datagram")));
            return Task.CompletedTask;
        }
    }

    private static LanMessageEnvelope Envelope(LanMessageType type, string text, string from = PhoneId, string to = HostId) =>
        new(type, Guid.NewGuid().ToString(), from, to, text, 1_000);

    private static LanMessageEnvelope PairRequest(byte[] phonePub) =>
        Envelope(LanMessageType.PairRequest, $$"""{"pub":"{{Convert.ToBase64String(phonePub)}}"}""");

    [Fact]
    public async Task Full_handshake_pairs_both_sides_with_the_same_key()
    {
        var transport = new CapturingTransport();
        var store = new InMemoryPairingStore();
        var host = new PairingService(store, transport, HostId);
        host.EnsureIdentity();

        string? shownPin = null;
        host.PinRequested += (_, pin) => shownPin = pin;

        // The phone generates an identity and offers it.
        var (phonePriv, phonePub) = PairingCrypto.GenerateIdentity();
        await host.HandleAsync(PairRequest(phonePub));

        // The host shows a 6-digit PIN derived from the shared secret.
        Assert.Matches("""^\d{6}$""", shownPin);

        // User accepts: the host answers with its identity key.
        await host.AcceptPairingAsync();
        var challenge = Assert.Single(transport.Sent, sent => sent.Envelope.Type == LanMessageType.PairChallenge).Envelope;
        var hostPub = Convert.FromBase64String(
            System.Text.Json.JsonSerializer.Deserialize<PairingPayloadShape>(challenge.Text)!.Pub!);

        // The phone derives its copy of the key, and proves it.
        var phoneKey = PairingCrypto.DeriveSharedKey(phonePriv, hostPub);
        Assert.Equal(PairingCrypto.PinFor(phoneKey, HostId, PhoneId), shownPin);
        var mac = PairingCrypto.ConfirmMac(phoneKey, PhoneId, HostId);
        var confirm = Envelope(
            LanMessageType.PairConfirm,
            $$"""{"mac":"{{Convert.ToBase64String(mac)}}"}""");
        await host.HandleAsync(confirm);

        // Both sides hold the same key; the host told the phone it is paired.
        Assert.True(host.IsPaired(PhoneId));
        Assert.Equal(phoneKey, host.KeyFor(PhoneId));
        var complete = Assert.Single(transport.Sent, sent => sent.Envelope.Type == LanMessageType.PairComplete).Envelope;
        Assert.Contains("\"ok\":true", complete.Text);
    }

    [Fact]
    public async Task A_wrong_mac_leaves_the_host_unpaired()
    {
        var transport = new CapturingTransport();
        var store = new InMemoryPairingStore();
        var host = new PairingService(store, transport, HostId);
        host.EnsureIdentity();

        var (_, phonePub) = PairingCrypto.GenerateIdentity();
        await host.HandleAsync(PairRequest(phonePub));
        await host.AcceptPairingAsync();

        // A phone that derived a different key (a MITM's relaid keys, or a bug).
        var (wrongPriv, _) = PairingCrypto.GenerateIdentity();
        using var ecdh = System.Security.Cryptography.ECDiffieHellman.Create();
        ecdh.ImportPkcs8PrivateKey(wrongPriv, out _);
        using var hostPublic = System.Security.Cryptography.ECDiffieHellman.Create();
        var challenge = Assert.Single(transport.Sent, sent => sent.Envelope.Type == LanMessageType.PairChallenge).Envelope;
        var hostPub = Convert.FromBase64String(
            System.Text.Json.JsonSerializer.Deserialize<PairingPayloadShape>(challenge.Text)!.Pub!);
        hostPublic.ImportSubjectPublicKeyInfo(hostPub, out _);
        var wrongKey = System.Security.Cryptography.HKDF.DeriveKey(
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            ZeroPad(ecdh.DeriveRawSecretAgreement(hostPublic.PublicKey)),
            32,
            System.Text.Encoding.UTF8.GetBytes("openagent-pair-v2"),
            System.Text.Encoding.UTF8.GetBytes("openagent/aes-256-gcm"));
        var mac = PairingCrypto.ConfirmMac(wrongKey, PhoneId, HostId);

        await host.HandleAsync(Envelope(LanMessageType.PairConfirm, $$"""{"mac":"{{Convert.ToBase64String(mac)}}"}"""));

        Assert.False(host.IsPaired(PhoneId));
    }

    [Fact]
    public void Pin_is_six_digits_and_depends_on_both_ids()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var pin = PairingCrypto.PinFor(key, HostId, PhoneId);
        Assert.Matches("""^\d{6}$""", pin);
        Assert.Equal(pin, PairingCrypto.PinFor(key, HostId, PhoneId));
        Assert.NotEqual(pin, PairingCrypto.PinFor(key, HostId, "android:Other"));
    }

    [Fact]
    public void Sealed_envelopes_round_trip_and_tampering_is_refused()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        var envelope = new LanMessageEnvelope(
            LanMessageType.Command, "id-1", PhoneId, HostId, "打开记事本", 1_000);

        var sealedEnvelope = EnvelopeCrypto.Seal(envelope, key);
        Assert.Equal(string.Empty, sealedEnvelope.Text);
        Assert.NotNull(sealedEnvelope.Cipher);
        Assert.NotNull(sealedEnvelope.Nonce);

        var decoded = LanMessageEnvelope.Decode(sealedEnvelope.Encode());
        Assert.True(EnvelopeCrypto.TryOpen(decoded!, key, out var opened));
        Assert.Equal("打开记事本", opened.Text);
        Assert.Null(opened.Cipher);

        // Flip one bit inside the first ciphertext character — always a valid
        // base64 alphabet char, always a different decoded byte, so the tag
        // check must fail rather than yield garbage.
        var tamperedBytes = sealedEnvelope.Encode();
        var tail = System.Text.Encoding.UTF8.GetString(tamperedBytes);
        var cipherStart = tail.IndexOf("\"cipher\":\"", StringComparison.Ordinal)
                          + "\"cipher\":\"".Length;
        tamperedBytes[cipherStart] ^= 0x01;
        var tampered = LanMessageEnvelope.Decode(tamperedBytes)!;
        Assert.False(EnvelopeCrypto.TryOpen(tampered, key, out _));
    }

    private static byte[] ZeroPad(byte[] value)
    {
        var padded = new byte[32];
        value.CopyTo(padded, 32 - value.Length);
        return padded;
    }

    private sealed class PairingPayloadShape
    {
        [System.Text.Json.Serialization.JsonPropertyName("pub")]
        public string? Pub { get; set; }
    }
}
