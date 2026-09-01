using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Routing;

using Organization.Infrastructure.Services;

namespace Organization.Application.Features.CanActorPerform;

public sealed record CanActorPerformQuery(
    Guid ActorUserId,
    Guid TenantId,
    string Permission,
    string ResourceType,
    Guid ResourceId,
    Guid? ResourceOwnerId,
    string? Classification,
    IReadOnlyList<string> ActorRoles
) : IRequest<Result<bool>>;

public sealed class CanActorPerformHandler : IRequestHandler<CanActorPerformQuery, Result<bool>>
{
    private readonly IAuthorizationEvaluator _evaluator;
    public CanActorPerformHandler(IAuthorizationEvaluator evaluator) => _evaluator = evaluator;
    public async Task<Result<bool>> HandleAsync(CanActorPerformQuery q, CancellationToken ct)
    {
        var req = new AuthorizationRequest(q.ActorUserId, q.TenantId, q.Permission, q.ResourceType, q.ResourceId, q.ResourceOwnerId, q.Classification, q.ActorRoles);
        var result = await _evaluator.EvaluateAsync(req, ct);
        return Result.Success(result.IsAllowed);
    }
}

public sealed class CanActorPerformEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Endpoint mapping deferred — see GetSubtreeEndpoint
    }
}