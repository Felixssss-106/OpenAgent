using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

/// <summary>The string vocabulary that crosses IAgentHost for 权限模式.</summary>
public sealed class PermissionModeChoicesTests
{
    [Fact]
    public void Normalize_falls_back_to_the_install_default_for_null() =>
        Assert.Equal("AskBeforeActions", PermissionModeChoices.Normalize(null));

    [Fact]
    public void Normalize_falls_back_to_the_install_default_for_unknown_values() =>
        Assert.Equal("AskBeforeActions", PermissionModeChoices.Normalize("FullControl"));

    [Fact]
    public void Normalize_never_lands_on_a_more_permissive_mode_by_accident()
    {
        // A typo must not widen what the agent may do (spec section 239).
        foreach (var typo in new[] { "fullaccess", "FullAcces", "AUTOAPPROVE", "auto" })
        {
            Assert.Equal("AskBeforeActions", PermissionModeChoices.Normalize(typo));
        }
    }

    [Fact]
    public void Normalize_passes_known_modes_through()
    {
        foreach (var mode in PermissionModeChoices.Modes)
        {
            Assert.Equal(mode, PermissionModeChoices.Normalize(mode));
        }
    }

    [Fact]
    public void Every_mode_has_a_label()
    {
        foreach (var mode in PermissionModeChoices.Modes)
        {
            Assert.False(string.IsNullOrWhiteSpace(PermissionModeChoices.LabelFor(mode)));
        }
    }

    [Fact]
    public void The_offer_covers_all_four_modes()
    {
        Assert.Equal(4, PermissionModeChoices.Modes.Count);
        Assert.Contains("ReadOnly", PermissionModeChoices.Modes);
        Assert.Contains("AskBeforeActions", PermissionModeChoices.Modes);
        Assert.Contains("AutoApprove", PermissionModeChoices.Modes);
        Assert.Contains("FullAccess", PermissionModeChoices.Modes);
    }
}
