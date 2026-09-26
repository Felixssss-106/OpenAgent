using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

public sealed class PlanViewMapperTests
{
    [Fact]
    public void Single_argument_is_just_its_value() =>
        // Artboards 03/04 draw the disclosure row as "file.list  D:\Downloads".
        Assert.Equal(
            @"D:\Downloads",
            PlanViewMapper.DescribeArguments(@"{""path"":""D:\\Downloads""}"));

    [Fact]
    public void Source_and_destination_read_as_a_move() =>
        // Artboard 05's approval card line.
        Assert.Equal(
            @"D:\Downloads\* → D:\Downloads\2026-09-25\",
            PlanViewMapper.DescribeArguments(
                @"{""source"":""D:\\Downloads\\*"",""destination"":""D:\\Downloads\\2026-09-25\\""}"));

    [Fact]
    public void Other_pairs_keep_their_keys() =>
        // A write would otherwise read as if one path became the other.
        Assert.Equal(
            @"path：D:\a.txt  ·  content：hi",
            PlanViewMapper.DescribeArguments(@"{""path"":""D:\\a.txt"",""content"":""hi""}"));

    [Fact]
    public void No_arguments_says_so() =>
        Assert.Equal("无参数", PlanViewMapper.DescribeArguments("{}"));

    [Fact]
    public void Booleans_and_numbers_are_not_drawn_as_json_literals()
    {
        Assert.Equal(
            @"path：D:\a.txt  ·  recursive：是",
            PlanViewMapper.DescribeArguments(@"{""path"":""D:\\a.txt"",""recursive"":true}"));
        Assert.Equal(
            "overwrite：否  ·  limit：50",
            PlanViewMapper.DescribeArguments(@"{""overwrite"":false,""limit"":50}"));
    }

    [Fact]
    public void Nested_values_are_dropped_rather_than_dumped() =>
        // The row is a summary line; an embedded object would push the path off screen.
        Assert.Equal(
            @"path：D:\a.txt  ·  content：hi",
            PlanViewMapper.DescribeArguments(
                @"{""path"":""D:\\a.txt"",""content"":""hi"",""meta"":{""a"":1}}"));

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void Unparseable_or_non_object_input_is_shown_raw(string arguments) =>
        // These are the parameters the user is being asked to confirm; showing them
        // unformatted beats showing nothing.
        Assert.Equal(arguments, PlanViewMapper.DescribeArguments(arguments));
}
