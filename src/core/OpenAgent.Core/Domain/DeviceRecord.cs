namespace OpenAgent.Core.Domain;

/// <summary>
/// Device identity (spec section 15). No MAC address, no IMEI, no phone number.
/// </summary>
public static class TrustStates
{
    public const string Untrusted = "untrusted";
    public const string Pending = "pending";
    public const string Trusted = "trusted";
    public const string Revoked = "revoked";
}

public sealed class DeviceRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string DeviceType { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;

    public string TrustState { get; set; } = TrustStates.Untrusted;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    /// <summary>SHA-256 of the device public key, shown during pairing.</summary>
    public string? KeyFingerprint { get; set; }
}
