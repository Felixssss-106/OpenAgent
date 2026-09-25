namespace OpenAgent.Core;

/// <summary>
/// Tool risk classification (spec section 18). The permission engine and every
/// approval card key off this value.
/// </summary>
public enum RiskLevel
{
    Safe = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public static class RiskLevels
{
    /// <summary>
    /// Batch approval may group operations at or below this level only
    /// (spec section 99).
    /// </summary>
    public const RiskLevel BatchApprovalCeiling = RiskLevel.Medium;

    /// <summary>
    /// Anything at or above this level can never be auto-approved, even in
    /// <c>AutoApprove</c> mode (spec section 17/42).
    /// </summary>
    public const RiskLevel NeverAutoApprove = RiskLevel.High;
}
