using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Application.Features.PromptVersions;

public sealed record ListPromptVersionsQuery(string OperationType, Guid TenantId, int Page = 1, int PageSize = 20) : IQuery<Result<PagedResult<PromptVersionDto>>>;
public sealed record PromptVersionDto(Guid PromptVersionId, string OperationType, int VersionNumber, string Template);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed class ListPromptVersionsHandler : IQueryHandler<ListPromptVersionsQuery, Result<PagedResult<PromptVersionDto>>>
{
    public Task<Result<PagedResult<PromptVersionDto>>> HandleAsync(ListPromptVersionsQuery q, CancellationToken ct)
    {
        var empty = new PagedResult<PromptVersionDto>(Array.Empty<PromptVersionDto>(), 0, q.Page, q.PageSize);
        return Task.FromResult(Result.Success(empty));
    }
}

public sealed record GetPromptVersionQuery(Guid PromptVersionId, Guid TenantId) : IQuery<Result<PromptVersionDto>>;
public sealed class GetPromptVersionHandler : IQueryHandler<GetPromptVersionQuery, Result<PromptVersionDto>>
{
    public Task<Result<PromptVersionDto>> HandleAsync(GetPromptVersionQuery q, CancellationToken ct)
    {
        return Task.FromResult(Result.Failure<PromptVersionDto>(Error.NotFound("Prompt.NotFound", "Prompt not found")));
    }
}
