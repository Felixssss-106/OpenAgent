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

    [Fact]
    public void Result_is_one_plain_text_line_per_fact() =>
        // Artboards 03/04 label the block 纯文本 and draw short lines, not one long
        // JSON value that truncates mid-fact.
        Assert.Equal(
            "os：Windows\nprocessorCount：12\ninteractive：是",
            PlanViewMapper.DescribeResult(
                @"{""os"":""Windows"",""processorCount"":12,""interactive"":true}"));

    [Fact]
    public void Empty_result_says_so()
    {
        Assert.Equal("完成", PlanViewMapper.DescribeResult(null));
        Assert.Equal("完成", PlanViewMapper.DescribeResult(string.Empty));
    }

    [Fact]
    public void Arrays_are_counted_and_sampled_rather_than_dumped()
    {
        var described = PlanViewMapper.DescribeResult(
            @"{""path"":""D:\\Downloads"",""items"":["
            + @"{""name"":""a.txt"",""size"":1},"
            + @"{""name"":""b.txt"",""size"":2},"
            + @"{""name"":""c.txt"",""size"":3},"
            + @"{""name"":""d.txt"",""size"":4}]}");

        Assert.Equal(
            "path：D:\\Downloads\n"
            + "items：4 项\n"
            + "  · name：a.txt · size：1\n"
            + "  · name：b.txt · size：2\n"
            + "  · name：c.txt · size：3\n"
            + "  …共 4 项",
            described);
    }

    [Fact]
    public void Nested_object_stays_on_its_own_line() =>
        Assert.Equal(
            "memory：used：1 · total：8",
            PlanViewMapper.DescribeResult(@"{""memory"":{""used"":1,""total"":8}}"));

    [Fact]
    public void Long_results_stop_at_the_line_budget()
    {
        var payload = "{\"a\":1,\"b\":2,\"c\":3,\"d\":4,\"e\":5,\"f\":6,\"g\":7,\"h\":8,"
            + "\"i\":9,\"j\":10,\"k\":11,\"l\":12,\"m\":13}";
        var lines = PlanViewMapper.DescribeResult(payload).Split('\n');

        // The cap counts emitted lines, and the ellipsis line is the cap's own marker.
        Assert.Equal(13, lines.Length);
        Assert.Equal("…", lines[^1]);
        Assert.Equal("a：1", lines[0]);
        Assert.Equal("l：12", lines[^2]);
    }
}
