using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Organization.Infrastructure.Persistence;

namespace Organization.Application.Features.ExplicitGrant;

public sealed record IssueExplicitGrantCommand(Guid TenantId, Guid GranteeUserId, Guid GrantedBy, string ResourceType, Guid ResourceId, string Permission, DateTime? ExpiresAt) : IRequest<Result<Guid>>;
public sealed record RevokeExplicitGrantCommand(Guid TenantId, Guid GrantId) : IRequest<Result>;

public sealed class IssueExplicitGrantHandler : IRequestHandler<IssueExplicitGrantCommand, Result<Guid>>
{
    private readonly OrganizationDbContext _db;
    public IssueExplicitGrantHandler(OrganizationDbContext db) => _db = db;
    public async Task<Result<Guid>> HandleAsync(IssueExplicitGrantCommand cmd, CancellationToken ct)
    {
        var grant = Domain.Aggregates.ExplicitGrant.Issue(cmd.TenantId, cmd.GranteeUserId, cmd.GrantedBy, cmd.ResourceType, cmd.ResourceId, cmd.Permission, cmd.ExpiresAt);
        _db.ExplicitGrants.Add(grant);
        await _db.SaveChangesAsync(ct);
        return grant.Id.Value;
    }
}

public sealed class RevokeExplicitGrantHandler : IRequestHandler<RevokeExplicitGrantCommand, Result>
{
    private readonly OrganizationDbContext _db;
    public RevokeExplicitGrantHandler(OrganizationDbContext db) => _db = db;
    public async Task<Result> HandleAsync(RevokeExplicitGrantCommand cmd, CancellationToken ct)
    {
        var grant = await _db.ExplicitGrants.FirstOrDefaultAsync(x => x.Id.Value == cmd.GrantId && x.TenantId == cmd.TenantId, ct);
        if (grant == null) return Result.Failure(Error.NotFound("ExplicitGrant.NotFound", $"Grant {cmd.GrantId} not found"));
        grant.Revoke();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}