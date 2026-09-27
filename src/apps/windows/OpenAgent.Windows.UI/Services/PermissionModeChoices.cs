using System.Linq;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// The permission modes the settings page offers, carried as the
/// <c>OpenAgent.Security</c> enum names in string form: the UI library does not
/// reference that project, so the enum itself never appears here. The adapter
/// translates on the other side of <see cref="IAgentHost.PermissionMode"/>.
/// </summary>
public static class PermissionModeChoices
{
    /// <summary>Every mode, in ascending aggressiveness order.</summary>
    public static readonly IReadOnlyList<string> Modes =
        new[] { "ReadOnly", "AskBeforeActions", "AutoApprove", "FullAccess" };

    public static string LabelFor(string mode) => mode switch
    {
        "ReadOnly" => "只读",
        "AskBeforeActions" => "请求批准",
        "AutoApprove" => "自动批准",
        "FullAccess" => "完全访问",
        _ => "请求批准",
    };

    /// <summary>Unknown or missing values fall back to the install default —
    /// never to a more permissive mode (spec section 239).</summary>
    public static string Normalize(string? mode) =>
        Modes.Any(m => m == mode) ? mode! : "AskBeforeActions";
}
