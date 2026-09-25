using System.Text.Json;

namespace OpenAgent.Tools;

/// <summary>
/// The catalogue the Agent reads. Core tools, plugin tools, MCP tools and
/// provider tools all land here so there is exactly one list (spec section 126).
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry()
    {
    }

    public ToolRegistry(IEnumerable<ITool> tools) => Register(tools);

    public void Register(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var id = tool.Definition.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A tool must declare a non-empty id.", nameof(tool));
        }

        if (_tools.ContainsKey(id))
        {
            throw new InvalidOperationException($"Tool '{id}' is already registered.");
        }

        _tools[id] = tool;
    }

    public ITool? Get(string id) => _tools.TryGetValue(id, out var tool) ? tool : null;

    public bool Contains(string id) => _tools.ContainsKey(id);

    public IReadOnlyCollection<ToolDefinition> All() =>
        _tools.Values.Select(t => t.Definition).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

    public ValidationResult ValidateArguments(string id, JsonElement arguments)
    {
        var tool = Get(id);
        if (tool is null)
        {
            return ValidationResult.Fail($"unknown tool '{id}'");
        }

        return ToolSchemaValidator.Validate(arguments, tool.Definition.InputSchemaJson);
    }
}
