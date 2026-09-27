using Microsoft.Data.Sqlite;
using OpenAgent.Transport.Pairing;

namespace OpenAgent.Storage.Repositories;

/// <summary>
/// SQLite persistence for the v2 pairing material (docs/protocol.md §4): one
/// PKCS#8 identity key for the install, one row per paired peer with the
/// derived AES-256-GCM key. The shared key is pairing state, not an OS secret —
/// it is re-derivable only with the private key that lives beside it.
/// </summary>
public sealed class PairingStore : IPairingStore
{
    private readonly SqliteDatabase _database;

    public PairingStore(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public byte[]? OwnPrivateKey()
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT private_key FROM pairing_identity WHERE id=1 LIMIT 1";
        var value = command.ExecuteScalar();
        return value is byte[] blob ? blob : null;
    }

    public void SaveOwnPrivateKey(byte[] privateKey)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pairing_identity (id, private_key) VALUES (1, $key)
            ON CONFLICT(id) DO UPDATE SET private_key=excluded.private_key
            """;
        command.Parameters.AddWithValue("$key", privateKey);
        command.ExecuteNonQuery();
    }

    public PairingRecord? Find(string peerId)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT peer_id, peer_pub, shared_key, paired_at_utc FROM pairing
            WHERE peer_id=$peer_id LIMIT 1
            """;
        command.Parameters.AddWithValue("$peer_id", peerId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PairingRecord(
            reader.GetString(0),
            reader.GetString(1),
            (byte[])reader.GetValue(2),
            SqliteDatabase.FromStorage(reader["paired_at_utc"]) ?? DateTimeOffset.MinValue);
    }

    public IReadOnlyList<PairingRecord> All()
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT peer_id, peer_pub, shared_key, paired_at_utc FROM pairing ORDER BY paired_at_utc";

        using var reader = command.ExecuteReader();
        var results = new List<PairingRecord>();
        while (reader.Read())
        {
            results.Add(new PairingRecord(
                reader.GetString(0),
                reader.GetString(1),
                (byte[])reader.GetValue(2),
                SqliteDatabase.FromStorage(reader["paired_at_utc"]) ?? DateTimeOffset.MinValue));
        }

        return results;
    }

    public void Save(PairingRecord pairing)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pairing (peer_id, peer_pub, shared_key, paired_at_utc)
            VALUES ($peer_id, $peer_pub, $key, $at)
            ON CONFLICT(peer_id) DO UPDATE SET
                peer_pub=excluded.peer_pub, shared_key=excluded.shared_key, paired_at_utc=excluded.paired_at_utc
            """;
        command.Parameters.AddWithValue("$peer_id", pairing.PeerId);
        command.Parameters.AddWithValue("$peer_pub", pairing.PeerPublicKey);
        command.Parameters.AddWithValue("$key", pairing.SharedKey);
        command.Parameters.AddWithValue("$at", SqliteDatabase.ToStorage(pairing.PairedAtUtc));
        command.ExecuteNonQuery();
    }
}
