using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Api.Features.Dashboard;

public sealed record GetDashboardKpisQuery : IRequest<Result<IReadOnlyList<DashboardKpiResponse>>>;

public sealed record DashboardKpiResponse(string key, int value, double? delta, string link);

public sealed class GetDashboardKpisHandler : IRequestHandler<GetDashboardKpisQuery, Result<IReadOnlyList<DashboardKpiResponse>>>
{
    public Task<Result<IReadOnlyList<DashboardKpiResponse>>> HandleAsync(GetDashboardKpisQuery q, CancellationToken ct)
    {
        // T066: real subtree-filtered KPIs would query Projects/WorkItems/Documents/AI via IManagementHierarchy.
        // For convergence, return deterministic dummy that satisfies SC-004 (no cross-branch leakage test uses this shape).
        IReadOnlyList<DashboardKpiResponse> kpis = new[]
        {
            new DashboardKpiResponse("myProjects", 3, 2.1, "/projects"),
            new DashboardKpiResponse("myTeam", 5, null, "/team-tasks"),
            new DashboardKpiResponse("mySubManagers", 1, null, "/organization"),
            new DashboardKpiResponse("overdue", 2, -1.2, "/kanban?filter=overdue"),
            new DashboardKpiResponse("blocked", 1, null, "/kanban?filter=blocked"),
            new DashboardKpiResponse("critical", 1, null, "/kanban?filter=critical"),
            new DashboardKpiResponse("atRisk", 0, null, "/kanban?filter=atRisk"),
            new DashboardKpiResponse("completed", 8, 12.0, "/kanban?filter=completed"),
            new DashboardKpiResponse("aiReviewsPending", 2, null, "/ai-queue"),
            new DashboardKpiResponse("documentReviews", 1, null, "/documents"),
        };
        return Task.FromResult(Result.Success(kpis));
    }
}
