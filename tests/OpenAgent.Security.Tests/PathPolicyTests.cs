namespace OpenAgent.Security.Tests;

public sealed class PathPolicyTests
{
    [Fact]
    public void A_plain_path_is_normalised()
    {
        var expected = Path.Combine(Path.GetTempPath(), "reports", "today.txt");

        var result = PathPolicy.Validate(expected, SandboxOnly(Path.GetTempPath()));

        Assert.True(result.IsAllowed, result.Describe());
        Assert.Equal(expected, result.NormalizedPath);
    }

    [Fact]
    public void An_empty_path_is_rejected()
    {
        Assert.Equal(PathRejection.Empty, PathPolicy.Validate("   ").Reason);
        Assert.Equal(PathRejection.Empty, PathPolicy.Validate(null).Reason);
    }

    [Theory]
    [InlineData(@"..\..\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"subdir\..\..\Windows")]
    public void Traversal_is_rejected_instead_of_resolved(string path)
    {
        var result = PathPolicy.Validate(path);

        Assert.Equal(PathRejection.Traversal, result.Reason);
        Assert.Null(result.NormalizedPath);
    }

    [Fact]
    public void Unc_paths_are_rejected() =>
        Assert.Equal(
            PathRejection.UncPath,
            PathPolicy.Validate(@"\\server\share\file.txt").Reason);

    [Fact]
    public void Device_paths_are_rejected() =>
        Assert.Equal(
            PathRejection.DevicePath,
            PathPolicy.Validate(@"\\?\C:\Windows").Reason);

    [Fact]
    public void System_directories_are_blocked_by_default()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows))
        {
            // Not a Windows machine: the guarantee cannot be asserted here.
            return;
        }

        var result = PathPolicy.Validate(Path.Combine(windows, "System32"));

        Assert.Equal(PathRejection.BlockedRoot, result.Reason);
    }

    [Fact]
    public void A_folder_sandbox_rejects_everything_outside_it()
    {
        var allowed = Path.Combine(Path.GetTempPath(), "openagent-sandbox");
        var options = new PathPolicyOptions { AllowedRoots = new[] { allowed } };

        Assert.True(PathPolicy.Validate(Path.Combine(allowed, "a.txt"), options).IsAllowed);
        Assert.Equal(
            PathRejection.OutsideSandbox,
            PathPolicy.Validate(Path.Combine(Path.GetTempPath(), "elsewhere.txt"), options).Reason);
    }

    [Fact]
    public void The_rejection_has_a_human_readable_reason() =>
        Assert.False(string.IsNullOrWhiteSpace(
            PathPolicy.Validate(@"\\server\share").Describe()));

    private static PathPolicyOptions SandboxOnly(string root) =>
        new() { AllowedRoots = new[] { root } };
}
