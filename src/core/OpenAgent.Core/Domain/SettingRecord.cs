namespace OpenAgent.Core.Domain;

public sealed class SettingRecord
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
