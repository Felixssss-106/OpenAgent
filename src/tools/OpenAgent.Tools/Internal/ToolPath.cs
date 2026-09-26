using OpenAgent.Security;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// The one place where a model-supplied path becomes a disk path: relative
/// paths are resolved against the call's working directory and everything is
/// then checked by <see cref="PathPolicy"/> (spec sections 39, 40, 241).
/// </summary>
internal static class ToolPath
{
    /// <summary>
    /// Resolves and validates <paramref name="rawPath"/>. A null or empty path
    /// falls back to the working directory of the call.
    /// </summary>
    internal static PathValidationResult Resolve(string? rawPath, ToolContext? context)
    {
        var workingDirectory = context?.WorkingDirectory;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.IsNullOrWhiteSpace(workingDirectory)
                ? PathValidationResult.Reject(PathRejection.Empty)
                : PathPolicy.Validate(workingDirectory);
        }

        var candidate = rawPath.Trim().Trim('"');

        if (!Path.IsPathRooted(candidate) && !string.IsNullOrWhiteSpace(workingDirectory))
        {
            try
            {
                // Combine only — never GetFullPath. Normalising here would
                // resolve ".." before the policy sees it, and a request for
                // "..\..\Windows" must be refused, not silently redirected
                // (spec section 39).
                candidate = Path.Combine(workingDirectory, candidate);
            }
            catch (ArgumentException)
            {
                return PathValidationResult.Reject(PathRejection.Invalid);
            }
        }

        return PathPolicy.Validate(candidate);
    }
}
