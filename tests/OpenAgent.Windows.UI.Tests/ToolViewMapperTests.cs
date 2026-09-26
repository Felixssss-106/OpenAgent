using OpenAgent.Core;
using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

public sealed class ToolViewMapperTests
{
    [Theory]
    [InlineData(RiskLevel.Safe, "只读")]
    [InlineData(RiskLevel.Low, "低风险")]
    [InlineData(RiskLevel.Medium, "中风险")]
    [InlineData(RiskLevel.High, "高风险")]
    [InlineData(RiskLevel.Critical, "极高风险")]
    public void RiskLabel_returns_expected_string(RiskLevel risk, string expected) =>
        Assert.Equal(expected, ToolViewMapper.RiskLabel(risk));

    [Theory]
    [InlineData(RiskLevel.Safe, "RiskSafeBrush")]
    [InlineData(RiskLevel.Low, "RiskLowBrush")]
    [InlineData(RiskLevel.Medium, "RiskMediumBrush")]
    [InlineData(RiskLevel.High, "RiskHighBrush")]
    [InlineData(RiskLevel.Critical, "RiskCriticalBrush")]
    public void RiskBrushKey_returns_expected_key(RiskLevel risk, string expected) =>
        Assert.Equal(expected, ToolViewMapper.RiskBrushKey(risk));

    [Theory]
    [InlineData(RiskLevel.Safe, "RiskSafeBgBrush")]
    [InlineData(RiskLevel.Low, "RiskLowBgBrush")]
    [InlineData(RiskLevel.Medium, "RiskMediumBgBrush")]
    [InlineData(RiskLevel.High, "RiskHighBgBrush")]
    [InlineData(RiskLevel.Critical, "RiskCriticalBgBrush")]
    public void RiskBackgroundKey_returns_expected_key(RiskLevel risk, string expected) =>
        Assert.Equal(expected, ToolViewMapper.RiskBackgroundKey(risk));

    [Theory]
    [InlineData("screen.capture", "\uE7B3")]
    [InlineData("file.delete", "\uE8A5")]
    [InlineData("process.list", "\uE9D5")]
    [InlineData("system.get_info", "\uE770")]
    [InlineData("app.launch", "\uE7AD")]
    [InlineData("shell.exec", "\uE756")]
    [InlineData("unknown.tool", "\uEC7A")]
    public void Glyph_returns_expected_icon(string toolId, string expected) =>
        Assert.Equal(expected, ToolViewMapper.Glyph(toolId));
}
