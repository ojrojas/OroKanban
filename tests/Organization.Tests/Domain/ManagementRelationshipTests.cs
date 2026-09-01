using Organization.Domain.Aggregates;
using Xunit;

namespace Organization.Tests.Domain;

public sealed class ManagementRelationshipTests
{
    [Fact]
    public void Create_WhenManagerEqualsSubordinate_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var ancestors = new List<Guid>();
        Assert.Throws<BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException>(() =>
            ManagementRelationship.Create(tenantId, managerId, managerId, "Manager", null, null, null, ancestors));
    }

    [Fact]
    public void Create_WhenCycleDetected_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var subordinateId = Guid.NewGuid();
        var ancestors = new List<Guid> { managerId }; // manager is ancestor of subordinate
        Assert.Throws<BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException>(() =>
            ManagementRelationship.Create(tenantId, managerId, subordinateId, "Manager", null, null, null, ancestors));
    }

    [Fact]
    public void Create_WhenValid_ShouldSucceedAndRaiseEvent()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var subordinateId = Guid.NewGuid();
        var ancestors = new List<Guid>();
        var rel = ManagementRelationship.Create(tenantId, managerId, subordinateId, "Manager", null, null, null, ancestors);
        Assert.Equal(managerId, rel.ManagerId);
        Assert.Single(rel.DomainEvents);
    }
}
