using Documents.Domain.Services;
namespace Documents.Infrastructure.Services;
public sealed class ClassificationPolicyService : IClassificationPolicy
{
    public System.Threading.Tasks.Task<(string Classification, string RuleVersion)> ClassifyAsync(ClassificationContext ctx, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(("Public", "v1"));
    public System.Collections.Generic.IReadOnlyList<string> AllowedLevels(System.Guid orgId) => new[] {"Public","Internal","Confidential","Restricted","HighlyRestricted"};
    public System.Threading.Tasks.Task<bool> IsAllowedAsync(string v, System.Guid orgId, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(AllowedLevels(orgId).Contains(v));
}
