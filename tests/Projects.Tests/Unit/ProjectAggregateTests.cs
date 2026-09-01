using FluentAssertions;

using Projects.Domain.Aggregates;
using Projects.Domain.Enumerations;

namespace Projects.Tests.Unit;

public class ProjectAggregateTests
{
    [Fact]
    public void Create_ShouldRaiseProjectCreated()
    {
        var tenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var p = Project.Create(tenant, "Revamp checkout", owner, manager, ProjectStatus.Active.Id, ProjectPriority.High.Id, Criticality.High.Id, null, null, null);
        p.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "ProjectCreatedDomainEvent");
        p.Name.Should().Be("Revamp checkout");
    }

    [Fact]
    public void AddMember_ShouldSucceed_AndDuplicateFails()
    {
        var p = Project.Create(Guid.NewGuid(), "Proj", Guid.NewGuid(), Guid.NewGuid(), ProjectStatus.Active.Id, ProjectPriority.High.Id, Criticality.High.Id, null, null, null);
        var user = Guid.NewGuid();
        p.AddMember(user, ProjectRole.Contributor.Id);
        p.Members.Should().HaveCount(1);
        var act = () => p.AddMember(user, ProjectRole.Contributor.Id);
        act.Should().Throw<BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException>();
    }
}