using BuildingBlocks.Kernel.Domain.Results;

using Projects.Domain.Enumerations;
using Projects.Domain.Services;

namespace Projects.Infrastructure.Services;

public sealed class AssignmentPolicy : IAssignmentPolicy
{
    private readonly Organization.Contracts.IManagementHierarchy _hierarchy;
    private readonly IProjectMembership _membership;
    private readonly IUserStateChecker _userChecker;

    public AssignmentPolicy(Organization.Contracts.IManagementHierarchy hierarchy, IProjectMembership membership, IUserStateChecker userChecker)
    {
        _hierarchy = hierarchy;
        _membership = membership;
        _userChecker = userChecker;
    }

    public async Task<Result> CanAssignAsync(Guid assignerId, Guid assigneeId, Guid projectId, Guid tenantId, int statusId, CancellationToken ct)
    {
        if (statusId == WorkItemStatus.Completed.Id)
            return Error.Validation("WorkItem.Completed", "Work item is completed");

        if (!await _userChecker.IsActiveAsync(assigneeId, ct))
            return Error.Validation("User.Inactive", "Assignee is inactive");

        // subtree OR shared membership (both must be members)
        var inSubtree = await _hierarchy.IsInSubtreeAsync(tenantId, assignerId, assigneeId, ct);
        if (inSubtree) return Result.Success();

        var assigneeMember = await _membership.IsMemberAsync(assigneeId, projectId, ct);
        var assignerMember = await _membership.IsMemberAsync(assignerId, projectId, ct);
        if (assigneeMember && assignerMember) return Result.Success();

        return Error.Forbidden("Assignment.Forbidden", "Assignee not in subtree and no shared project membership");
    }
}

public sealed class DefaultUserStateChecker : IUserStateChecker
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken ct) => Task.FromResult(true);
}

public sealed class ProjectMembershipService : IProjectMembership
{
    private readonly Persistence.ProjectsDbContext _db;
    public ProjectMembershipService(Persistence.ProjectsDbContext db) => _db = db;

    public async Task<bool> IsMemberAsync(Guid userId, Guid projectId, CancellationToken ct) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
            _db.Projects.Where(p => p.Id == new Projects.Domain.Ids.ProjectId(projectId)).SelectMany(p => p.Members).Where(m => m.UserId == userId), ct);

    public async Task<IReadOnlySet<Guid>> GetProjectIdsForUserAsync(Guid userId, CancellationToken ct)
    {
        // not trivial with owned; scan projects and filter
        var ids = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _db.Projects.Where(p => p.Members.Any(m => m.UserId == userId)).Select(p => p.Id.Value), ct);
        return ids.ToHashSet();
    }
}