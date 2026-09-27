using System.Security.Cryptography;
using System.Text.Json;

namespace OpenAgent.Transport.Pairing;

/// <summary>
/// The host side of the v2 pairing handshake (docs/protocol.md §4.2): the phone
/// offers its identity key, the user compares the derived PIN on both screens,
/// the phone proves it derived the same key, and both sides store the pairing.
/// Plaintext by design — the PIN never crosses the wire, so a man-in-the-middle
/// relaying keys cannot make the two screens agree.
/// </summary>
public sealed class PairingService
{
    private readonly IPairingStore _store;
    private readonly ITransport _transport;
    private readonly string _hostId;
    private readonly TimeProvider _clock;
    private PendingPairing? _pending;

    /// <summary>A pair_request arrived: show this PIN and ask the user to accept.</summary>
    public event EventHandler<string>? PinRequested;

    /// <summary>The user declined the dialog (or it timed out): tell the phone nothing and forget the request.</summary>
    public event EventHandler<string>? PairingDeclined;

    /// <summary>A peer completed the handshake and is now paired.</summary>
    public event EventHandler<string>? PairingCompleted;

    public PairingService(IPairingStore store, ITransport transport, string hostId, TimeProvider? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _hostId = hostId ?? throw new ArgumentNullException(nameof(hostId));
        _clock = clock ?? TimeProvider.System;
    }

    public bool IsPaired(string peerId) => _store.Find(peerId) is not null;

    /// <summary>The AES-256-GCM key shared with <paramref name="peerId"/>, or null before pairing.</summary>
    public byte[]? KeyFor(string peerId) => _store.Find(peerId)?.SharedKey;

    /// <summary>Generates the install's identity key on first run; later calls keep the stored one.</summary>
    public void EnsureIdentity()
    {
        if (_store.OwnPrivateKey() is null)
        {
            var (priv, _) = PairingCrypto.GenerateIdentity();
            _store.SaveOwnPrivateKey(priv);
        }
    }

    /// <summary>
    /// Consumes a pairing envelope. True when the envelope was a pairing message
    /// and is fully handled — the caller must not treat it as application
    /// traffic. Everything else flows on to the normal pipeline.
    /// </summary>
    public async Task<bool> HandleAsync(LanMessageEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case LanMessageType.PairRequest:
                await OnPairRequestAsync(envelope);
                return true;
            case LanMessageType.PairConfirm:
                await OnPairConfirmAsync(envelope);
                return true;
            case LanMessageType.PairChallenge:
            case LanMessageType.PairComplete:
                // The host only answers; the phone initiates. A stray challenge
                // or completion is noise from a peer that lost its state.
                return true;
            default:
                return false;
        }
    }

    /// <summary>The user accepted the PIN dialog: answer with our identity key.</summary>
    public async Task AcceptPairingAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var pending = _pending;
        var pub = Convert.ToBase64String(OwnPublicKey());
        var challenge = new LanMessageEnvelope(
            LanMessageType.PairChallenge,
            Guid.NewGuid().ToString(),
            _hostId,
            pending.PeerId,
            JsonSerializer.Serialize(new PairingPayload { Pub = pub }),
            _clock.GetTimestamp() / TimeSpan.TicksPerMillisecond);
        await _transport.SendAsync(pending.PeerId, challenge.Encode());
    }

    /// <summary>The user declined: forget the pending request; the phone stays unpaired.</summary>
    public void DeclinePairing()
    {
        if (_pending is not null)
        {
            PairingDeclined?.Invoke(this, _pending.PeerId);
            _pending = null;
        }
    }

    private async Task OnPairRequestAsync(LanMessageEnvelope request)
    {
        var payload = JsonSerializer.Deserialize<PairingPayload>(request.Text);
        if (payload?.Pub is null)
        {
            return;
        }

        byte[] peerPub;
        byte[] own;
        try
        {
            peerPub = Convert.FromBase64String(payload.Pub);
            own = OwnPrivateKey();
        }
        catch (FormatException)
        {
            return;
        }

        var key = PairingCrypto.DeriveSharedKey(own, peerPub);
        _pending = new PendingPairing(request.From, peerPub, key);
        PinRequested?.Invoke(this, PairingCrypto.PinFor(key, _hostId, request.From));
    }

    private async Task OnPairConfirmAsync(LanMessageEnvelope confirm)
    {
        if (_pending is null || confirm.From != _pending.PeerId)
        {
            return;
        }

        PairingPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PairingPayload>(confirm.Text);
        }
        catch (JsonException)
        {
            return;
        }

        if (payload?.Mac is null)
        {
            return;
        }

        byte[] mac;
        try
        {
            mac = Convert.FromBase64String(payload.Mac);
        }
        catch (FormatException)
        {
            return;
        }

        var expected = PairingCrypto.ConfirmMac(_pending.Key, confirm.From, _hostId);
        if (!CryptographicOperations.FixedTimeEquals(mac, expected))
        {
            // Wrong key derivation on the phone (or an active attack): refuse
            // silently and forget the pending state; the user can re-pair.
            _pending = null;
            return;
        }

        _store.Save(new PairingRecord(
            confirm.From,
            Convert.ToBase64String(_pending.PeerPublicKey),
            _pending.Key,
            _clock.GetUtcNow()));
        _pending = null;

        var complete = new LanMessageEnvelope(
            LanMessageType.PairComplete,
            Guid.NewGuid().ToString(),
            _hostId,
            confirm.From,
            JsonSerializer.Serialize(new PairingPayload { Ok = true }),
            _clock.GetTimestamp() / TimeSpan.TicksPerMillisecond);
        await _transport.SendAsync(confirm.From, complete.Encode());
        PairingCompleted?.Invoke(this, confirm.From);
    }

    private byte[] OwnPublicKey()
    {
        using var ecdh = System.Security.Cryptography.ECDiffieHellman.Create();
        ecdh.ImportPkcs8PrivateKey(OwnPrivateKey(), out _);
        return ecdh.ExportSubjectPublicKeyInfo();
    }

    private byte[] OwnPrivateKey() =>
        _store.OwnPrivateKey() ?? throw new InvalidOperationException(
            "no pairing identity: the host must generate one at startup (PairingService.EnsureIdentity)");

    private sealed record PendingPairing(string PeerId, byte[] PeerPublicKey, byte[] Key);
}

/// <summary>
/// The JSON payload carried in pair_* envelopes' text field. Wire names are
/// lowercase (docs/protocol.md §4.2); the record is public because the Android
/// client mirrors it exactly.
/// </summary>
public sealed record PairingPayload(
    [property: System.Text.Json.Serialization.JsonPropertyName("pub")] string? Pub = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("mac")] string? Mac = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("ok")] bool Ok = false);
