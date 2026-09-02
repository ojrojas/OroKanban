using Documents.Domain.Services;
namespace Documents.Infrastructure.Services;
public sealed class DocumentAccessPolicyService : IDocumentAccessPolicy
{
    private readonly DocumentAccessPolicy _inner = new();
    public Task<AccessDecision> EvaluateAsync(AccessContext ctx, CancellationToken ct) => _inner.EvaluateAsync(ctx, ct);
}
