using Organization.Domain.Aggregates;

using Xunit;

namespace Organization.Tests.Domain;

public sealed class ExplicitGrantTests
{
    [Fact]
    public void IsExpired_WhenFuture_ShouldBeFalse()
    {
        var grant = ExplicitGrant.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WorkItem", Guid.NewGuid(), "workitem.read", DateTime.UtcNow.AddHours(1));
        Assert.False(grant.IsExpired(DateTime.UtcNow));
        Assert.True(grant.IsSatisfiedBy(grant.TenantId, grant.GranteeUserId, grant.ResourceType, grant.ResourceId, grant.Permission, DateTime.UtcNow));
    }

    [Fact]
    public void IsExpired_WhenPast_ShouldBeTrue()
    {
        var grant = ExplicitGrant.Issue(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WorkItem", Guid.NewGuid(), "workitem.read", DateTime.UtcNow.AddHours(-1));
        Assert.True(grant.IsExpired(DateTime.UtcNow));
        Assert.False(grant.IsSatisfiedBy(grant.TenantId, grant.GranteeUserId, grant.ResourceType, grant.ResourceId, grant.Permission, DateTime.UtcNow));
    }

    [Fact]
    public void IsSatisfiedBy_WhenNullExpiry_ShouldBeTrue()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var grant = ExplicitGrant.Issue(tenantId, userId, Guid.NewGuid(), "WorkItem", resourceId, "workitem.read", null);
        Assert.True(grant.IsSatisfiedBy(tenantId, userId, "WorkItem", resourceId, "workitem.read", DateTime.UtcNow));
        Assert.False(grant.IsSatisfiedBy(tenantId, userId, "WorkItem", resourceId, "workitem.read", DateTime.UtcNow.AddYears(1)) == false); // null expiry never expires
    }
}