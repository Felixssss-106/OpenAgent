using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Security;
using OpenAgent.Tools;

namespace OpenAgent.Tools.Tests;

/// <summary>
/// The whole built-in catalogue: ids, schemas and the risk contract the
/// permission engine keys off (spec sections 18, 125, 126, 131).
/// </summary>
public sealed class BuiltInToolCatalogTests
{
    [Fact]
    public void Every_built_in_tool_registers_with_a_dotted_id()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());

        Assert.NotEmpty(registry.All());
        foreach (var definition in registry.All())
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
            Assert.Contains('.', definition.Id);
            Assert.Equal(definition.Id, definition.Id.Trim().ToLowerInvariant());
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.NotEmpty(definition.Permissions);
        }
    }

    [Fact]
    public void Every_schema_validates_its_own_required_arguments()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());

        foreach (var definition in registry.All())
        {
            var empty = ToolSchemaValidator.Validate(
                JsonDocument.Parse("{}").RootElement, definition.InputSchemaJson);

            var required = ReadRequired(definition.InputSchemaJson);
            Assert.Equal(required.Count == 0, empty.IsValid);
        }
    }

    [Fact]
    public void Read_only_tools_are_safe_and_reversible()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());

        foreach (var definition in registry.All())
        {
            if (definition.Risk == RiskLevel.Safe)
            {
                Assert.True(definition.Reversible, $"{definition.Id} is read-only, so it must be reversible");
            }
        }
    }

    [Fact]
    public void Irreversible_tools_are_marked_as_such_and_ask_first()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());
        var expected = new[]
        {
            "app.close",
            "app.launch",
            "file.copy",
            "file.delete",
            "file.move",
            "file.write",
            "process.terminate",
        };

        foreach (var id in expected)
        {
            var definition = registry.Get(id)!.Definition;
            Assert.False(definition.Reversible, $"{id} must be marked irreversible");
            Assert.True(definition.RequiresApproval, $"{id} must require approval");
            Assert.True(definition.Risk >= RiskLevel.Low, $"{id} must be at least low risk");
        }
    }

    [Fact]
    public void Destructive_tools_are_never_batch_approved()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());

        Assert.False(PermissionService.IsBatchApprovable(registry.Get("file.delete")!.Definition.Risk));
        Assert.False(PermissionService.IsBatchApprovable(registry.Get("process.terminate")!.Definition.Risk));
    }

    [Fact]
    public void The_read_only_permission_mode_denies_every_mutating_tool()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());
        var permissions = new PermissionService();

        foreach (var definition in registry.All())
        {
            var decision = permissions.Evaluate(
                new PermissionRequest(
                    definition.Id,
                    definition.Risk,
                    definition.Reversible,
                    null),
                PermissionMode.ReadOnly);

            var expected = definition.Risk == RiskLevel.Safe ||
                           (definition.Risk == RiskLevel.Low && definition.Reversible);

            Assert.Equal(
                expected ? PermissionDecision.Allow : PermissionDecision.Deny,
                decision);
        }
    }

    [Fact]
    public void Required_arguments_are_reported_by_the_registry()
    {
        var registry = new ToolRegistry(BuiltInTools.Create());

        Assert.False(registry.ValidateArguments("file.read", JsonDocument.Parse("{}").RootElement).IsValid);
        Assert.False(registry.ValidateArguments("file.write", JsonDocument.Parse("{\"path\":\"x\"}").RootElement).IsValid);
        Assert.True(registry.ValidateArguments("file.read", JsonDocument.Parse("{\"path\":\"x\"}").RootElement).IsValid);
        Assert.True(registry.ValidateArguments("file.list", JsonDocument.Parse("{}").RootElement).IsValid);
    }

    private static List<string> ReadRequired(string schemaJson)
    {
        using var document = JsonDocument.Parse(schemaJson);
        if (!document.RootElement.TryGetProperty("required", out var required) ||
            required.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return required.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }
}
