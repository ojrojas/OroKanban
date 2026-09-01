using FluentAssertions;

using NSubstitute;

using Projects.Domain.Enumerations;
using Projects.Domain.Services;
using Projects.Infrastructure.Services;

namespace Projects.Tests.Unit;

public class AssignmentPolicyMatrixTests
{
    [Fact]
    public async Task SubtreeMember_ShouldAllow()
    {
        var hierarchy = Substitute.For<Organization.Contracts.IManagementHierarchy>();
        hierarchy.IsInSubtreeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var membership = Substitute.For<IProjectMembership>();
        var userChecker = Substitute.For<IUserStateChecker>();
        userChecker.IsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var policy = new AssignmentPolicy(hierarchy, membership, userChecker);
        var result = await policy.CanAssignAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkItemStatus.Backlog.Id, default);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Completed_ShouldReject()
    {
        var hierarchy = Substitute.For<Organization.Contracts.IManagementHierarchy>();
        var membership = Substitute.For<IProjectMembership>();
        var userChecker = Substitute.For<IUserStateChecker>();
        userChecker.IsActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var policy = new AssignmentPolicy(hierarchy, membership, userChecker);
        var result = await policy.CanAssignAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkItemStatus.Completed.Id, default);
        result.IsFailure.Should().BeTrue();
    }
}