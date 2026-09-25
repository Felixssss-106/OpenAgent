namespace OpenAgent.Shared.Protocol;

/// <summary>
/// Wire protocol version. Receivers must accept N and N-1 (spec section 226).
/// </summary>
public static class ProtocolVersion
{
    public const int Current = 1;

    public const int MinimumSupported = 1;

    public static bool IsSupported(int version) => version >= MinimumSupported && version <= Current;
}
