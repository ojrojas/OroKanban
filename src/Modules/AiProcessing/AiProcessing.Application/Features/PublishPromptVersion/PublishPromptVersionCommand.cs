using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Application.Features.PublishPromptVersion;

public sealed record PublishPromptVersionCommand(string OperationType, string Template, Guid TenantId, Guid ActorId) : ICommand<Result<PromptVersionResponse>>;
public sealed record PromptVersionResponse(Guid PromptVersionId, string OperationType, int VersionNumber);

public sealed class PublishPromptVersionValidator : IValidator<PublishPromptVersionCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(PublishPromptVersionCommand r, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(r.Template) || !r.Template.Contains("{{content}}")) failures.Add(new ValidationFailure(nameof(r.Template), "Template must contain {{content}}"));
        if (string.IsNullOrWhiteSpace(r.OperationType)) failures.Add(new ValidationFailure(nameof(r.OperationType), "OperationType required"));
        return Task.FromResult((IReadOnlyCollection<ValidationFailure>)failures);
    }
}

public sealed class PublishPromptVersionHandler : ICommandHandler<PublishPromptVersionCommand, Result<PromptVersionResponse>>
{
    public Task<Result<PromptVersionResponse>> HandleAsync(PublishPromptVersionCommand cmd, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        return Task.FromResult(Result.Success(new PromptVersionResponse(id, cmd.OperationType, 1)));
    }
}
