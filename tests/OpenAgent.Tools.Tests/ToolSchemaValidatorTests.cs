using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Tools;
using OpenAgent.Tools.System;

namespace OpenAgent.Tools.Tests;

public sealed class ToolSchemaValidatorTests
{
    private const string Schema = """
    {"type":"object","properties":{"path":{"type":"string"},"limit":{"type":"integer"}},"required":["path"]}
    """;

    [Fact]
    public void A_valid_call_passes()
    {
        var args = JsonDocument.Parse("{\"path\":\"D:/tmp\",\"limit\":5}").RootElement;

        Assert.True(ToolSchemaValidator.Validate(args, Schema).IsValid);
    }

    [Fact]
    public void A_missing_required_argument_is_reported()
    {
        var args = JsonDocument.Parse("{\"limit\":5}").RootElement;
        var result = ToolSchemaValidator.Validate(args, Schema);

        Assert.False(result.IsValid);
        Assert.Contains("path", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrong_type_is_reported()
    {
        var args = JsonDocument.Parse("{\"path\":42}").RootElement;
        var result = ToolSchemaValidator.Validate(args, Schema);

        Assert.False(result.IsValid);
        Assert.Contains("string", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_non_integer_number_is_rejected_for_integer()
    {
        var args = JsonDocument.Parse("{\"path\":\"p\",\"limit\":1.5}").RootElement;

        Assert.False(ToolSchemaValidator.Validate(args, Schema).IsValid);
    }

    [Fact]
    public void Non_object_arguments_are_rejected()
    {
        var args = JsonDocument.Parse("[1,2,3]").RootElement;

        Assert.False(ToolSchemaValidator.Validate(args, Schema).IsValid);
    }

    [Fact]
    public void Unknown_tools_are_rejected_by_the_registry()
    {
        var registry = new ToolRegistry();

        var result = registry.ValidateArguments("does.not.exist", JsonDocument.Parse("{}").RootElement);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void The_registry_lists_and_resolves_registered_tools()
    {
        var registry = new ToolRegistry(new ITool[] { new SystemGetInfoTool(), new ProcessListTool() });

        Assert.Equal(2, registry.All().Count);
        Assert.NotNull(registry.Get("system.get_info"));
        Assert.True(registry.Contains("process.list"));
        Assert.Null(registry.Get("nope"));
    }

    [Fact]
    public void Registering_the_same_tool_twice_is_refused()
    {
        var registry = new ToolRegistry();
        registry.Register(new SystemGetInfoTool());

        Assert.Throws<InvalidOperationException>(() => registry.Register(new SystemGetInfoTool()));
    }

    [Fact]
    public void Every_built_in_tool_declares_a_real_risk_level()
    {
        var registry = new ToolRegistry(new ITool[] { new SystemGetInfoTool(), new ProcessListTool(), new AppLaunchTool() });

        foreach (var definition in registry.All())
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.True(Enum.IsDefined(definition.Risk));
            Assert.False(string.IsNullOrWhiteSpace(definition.InputSchemaJson));
        }
    }
}
