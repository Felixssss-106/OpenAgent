namespace OpenAgent.Transport.Pairing;

/// <summary>One completed pairing: the peer, its identity key and the derived session key.</summary>
public sealed record PairingRecord(
    string PeerId,
    string PeerPublicKey,
    byte[] SharedKey,
    DateTimeOffset PairedAtUtc);

/// <summary>
/// Where pairing material lives. The transport defines the shape; storage owns
/// the persistence (SQLite on Windows, SharedPreferences on Android), and tests
/// supply an in-memory one. The own private key never leaves this interface.
/// </summary>
public interface IPairingStore
{
    /// <summary>The install's PKCS#8 identity key, or null before the first pairing.</summary>
    byte[]? OwnPrivateKey();

    void SaveOwnPrivateKey(byte[] privateKey);

    PairingRecord? Find(string peerId);

    IReadOnlyList<PairingRecord> All();

    void Save(PairingRecord pairing);
}

/// <summary>Trivial in-process store for tests and for seeding.</summary>
public sealed class InMemoryPairingStore : IPairingStore
{
    private byte[]? _ownPrivateKey;
    private readonly Dictionary<string, PairingRecord> _pairings = new(StringComparer.Ordinal);

    public byte[]? OwnPrivateKey() => _ownPrivateKey;

    public void SaveOwnPrivateKey(byte[] privateKey) => _ownPrivateKey = privateKey;

    public PairingRecord? Find(string peerId) =>
        _pairings.TryGetValue(peerId, out var pairing) ? pairing : null;

    public IReadOnlyList<PairingRecord> All() => _pairings.Values.ToArray();

    public void Save(PairingRecord pairing) => _pairings[pairing.PeerId] = pairing;
}
