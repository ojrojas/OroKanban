using Organization.Domain.Rules;

using Xunit;

namespace Organization.Tests.Domain;

public sealed class SubtreeCannotContainManagerRuleTests
{
    [Fact]
    public void IsBroken_WhenManagerIsInAncestors_ShouldBeTrue()
    {
        var managerId = Guid.NewGuid();
        var subordinateId = Guid.NewGuid();
        var ancestors = new List<Guid> { Guid.NewGuid(), managerId, Guid.NewGuid() };
        var rule = new SubtreeCannotContainManagerRule(managerId, subordinateId, ancestors);
        Assert.True(rule.IsBroken());
    }

    [Fact]
    public void IsBroken_WhenManagerNotInAncestors_ShouldBeFalse()
    {
        var managerId = Guid.NewGuid();
        var subordinateId = Guid.NewGuid();
        var ancestors = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var rule = new SubtreeCannotContainManagerRule(managerId, subordinateId, ancestors);
        Assert.False(rule.IsBroken());
    }

    [Fact]
    public void ManagerCannotBeSubordinate_WhenSameId_ShouldBeBroken()
    {
        var id = Guid.NewGuid();
        var rule = new ManagerCannotBeSubordinateRule(id, id);
        Assert.True(rule.IsBroken());
    }
}