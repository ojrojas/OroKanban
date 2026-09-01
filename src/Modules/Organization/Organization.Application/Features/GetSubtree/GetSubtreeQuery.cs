using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Routing;

using Organization.Contracts;

namespace Organization.Application.Features.GetSubtree;

public sealed record GetSubtreeQuery(Guid TenantId, Guid ManagerId) : IRequest<Result<IReadOnlyList<Guid>>>;
public sealed record WhoReportsToMeQuery(Guid TenantId, Guid ManagerId) : IRequest<Result<IReadOnlyList<Guid>>>;
public sealed record GetAncestorsQuery(Guid TenantId, Guid UserId) : IRequest<Result<IReadOnlyList<Guid>>>;

public sealed class GetSubtreeHandler : IRequestHandler<GetSubtreeQuery, Result<IReadOnlyList<Guid>>>
{
    private readonly IManagementHierarchy _hierarchy;
    public GetSubtreeHandler(IManagementHierarchy hierarchy) => _hierarchy = hierarchy;
    public async Task<Result<IReadOnlyList<Guid>>> HandleAsync(GetSubtreeQuery q, CancellationToken ct)
    {
        var result = await _hierarchy.GetSubtreeAsync(q.TenantId, q.ManagerId, ct);
        return Result.Success(result);
    }
}

public sealed class WhoReportsToMeHandler : IRequestHandler<WhoReportsToMeQuery, Result<IReadOnlyList<Guid>>>
{
    private readonly IManagementHierarchy _hierarchy;
    public WhoReportsToMeHandler(IManagementHierarchy hierarchy) => _hierarchy = hierarchy;
    public async Task<Result<IReadOnlyList<Guid>>> HandleAsync(WhoReportsToMeQuery q, CancellationToken ct)
    {
        var result = await _hierarchy.GetSubtreeAsync(q.TenantId, q.ManagerId, ct);
        return Result.Success(result);
    }
}

public sealed class GetAncestorsHandler : IRequestHandler<GetAncestorsQuery, Result<IReadOnlyList<Guid>>>
{
    private readonly IManagementHierarchy _hierarchy;
    public GetAncestorsHandler(IManagementHierarchy hierarchy) => _hierarchy = hierarchy;
    public async Task<Result<IReadOnlyList<Guid>>> HandleAsync(GetAncestorsQuery q, CancellationToken ct)
    {
        var result = await _hierarchy.GetAncestorsAsync(q.TenantId, q.UserId, ct);
        return Result.Success(result);
    }
}

public sealed class GetSubtreeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Endpoint mapping deferred — registered via Api composition at foundation stage
    }
}