using FluentAssertions;

using Projects.Domain.ValueObjects;

namespace Projects.Tests.Unit;

public class ValueObjectTests
{
    [Fact]
    public void Effort_Negative_ShouldThrow()
    {
        var act = () => Effort.FromHours(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Progress_OutOfRange_ShouldThrow()
    {
        var act = () => ProgressValue.FromPercent(101);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("kanban", "kanban")]
    [InlineData("  KANBAN  ", "kanban")]
    public void Tag_Normalization_ShouldLowercaseAndTrim(string input, string expected)
    {
        var tag = Tag.Create(input);
        tag.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad tag")]
    [InlineData("toolongtoolongtoolongtoolongtoolongtoolongtoolongtoolong")]
    public void Tag_Invalid_ShouldThrow(string input)
    {
        var act = () => Tag.Create(input);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void DueDate_IsOverdue_ShouldBeTrueWhenPast()
    {
        var dd = DueDate.From(DateTime.UtcNow.AddDays(-1));
        dd.IsOverdue(DateTime.UtcNow, "InProgress").Should().BeTrue();
        dd.IsOverdue(DateTime.UtcNow, "Completed").Should().BeFalse();
    }
}