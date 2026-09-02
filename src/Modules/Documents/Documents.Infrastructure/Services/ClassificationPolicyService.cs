using Documents.Domain.Services;
namespace Documents.Infrastructure.Services;
public sealed class ClassificationPolicyService : IClassificationPolicy
{
    public Task<(string Classification, string RuleVersion)> ClassifyAsync(ClassificationContext ctx, CancellationToken ct) => Task.FromResult(("Public", "v1"));
    public IReadOnlyList<string> AllowedLevels(Guid orgId) => new[] {"Public","Internal","Confidential","Restricted","HighlyRestricted"};
    public Task<bool> IsAllowedAsync(string v, Guid orgId, CancellationToken ct) => Task.FromResult(AllowedLevels(orgId).Contains(v));
}
