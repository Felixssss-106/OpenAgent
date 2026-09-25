namespace OpenAgent.Security;

/// <summary>
/// The four permission modes (spec section 17). The default after install is
/// <see cref="AskBeforeActions"/> — never full access (spec section 239).
/// </summary>
public enum PermissionMode
{
    ReadOnly = 0,
    AskBeforeActions = 1,
    AutoApprove = 2,
    FullAccess = 3,
}
