using Documents.Domain.Services;
namespace Documents.Infrastructure.Services;
public sealed class DocumentAccessPolicyService : IDocumentAccessPolicy
{
    private readonly DocumentAccessPolicy _inner = new();
    public System.Threading.Tasks.Task<AccessDecision> EvaluateAsync(AccessContext ctx, System.Threading.CancellationToken ct) => _inner.EvaluateAsync(ctx, ct);
}
