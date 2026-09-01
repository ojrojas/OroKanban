using FluentAssertions;

using Projects.Domain.Enumerations;

namespace Projects.Tests.Unit;

public class WorkItemTypeEnumerationTests
{
    [Fact]
    public void AllSeededTypes_ShouldResolve()
    {
        WorkItemType.FromName("Epic").Id.Should().Be(1);
        WorkItemType.FromName("Feature").Id.Should().Be(2);
        WorkItemType.FromName("Task").Id.Should().Be(3);
        WorkItemType.FromName("Subtask").Id.Should().Be(4);
    }

    [Fact]
    public void UnknownType_ShouldThrow()
    {
        var act = () => WorkItemType.FromName("UnknownType");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}