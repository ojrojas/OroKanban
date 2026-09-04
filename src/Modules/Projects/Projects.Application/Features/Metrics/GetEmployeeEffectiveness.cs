using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.Metrics;

public sealed record GetEmployeeEffectivenessQuery(Guid UserId, Guid TenantId) : IQuery<Result<EmployeeEffectivenessResponse>>;
public sealed record EmployeeEffectivenessResponse(Guid UserId, int Score, IReadOnlyList<string> Penalties, int TotalTasks, int CompletedLate, int OverdueOpen, int Reopened, decimal HoursExtra);

public sealed class GetEmployeeEffectivenessHandler(ProjectsDbContext db) : IQueryHandler<GetEmployeeEffectivenessQuery, Result<EmployeeEffectivenessResponse>>
{
    public async Task<Result<EmployeeEffectivenessResponse>> HandleAsync(GetEmployeeEffectivenessQuery q, CancellationToken ct)
    {
        var items = await db.WorkItems.AsNoTracking().Where(w => w.TenantId == q.TenantId && w.ResponsibleId == q.UserId).ToListAsync(ct);
        var total = items.Count;
        var penalties = new List<string>();
        var score = 100;
        var completedLate = 0;
        var overdueOpen = 0;
        var reopened = 0;
        decimal hoursExtra = 0;
        var now = DateTime.UtcNow;
        foreach (var w in items)
        {
            if (w.ReopenedCount > 0) { var p = 5 * w.ReopenedCount; score -= p; penalties.Add($"Reapertura {w.Title}: -{p}"); reopened += w.ReopenedCount; }
            if (w.DueDate.HasValue)
            {
                if (w.StatusId == WorkItemStatus.Completed.Id && w.CompletedAt.HasValue && w.CompletedAt.Value > w.DueDate.Value)
                {
                    var days = (int)Math.Ceiling((w.CompletedAt.Value - w.DueDate.Value).TotalDays);
                    var p = 2 * days; score -= p; penalties.Add($"Entrega tarde {w.Title}: -{p} ({days}d)"); completedLate++;
                }
                else if (w.StatusId != WorkItemStatus.Completed.Id && w.DueDate.Value < now)
                {
                    var days = (int)Math.Ceiling((now - w.DueDate.Value).TotalDays);
                    var p = (int)Math.Ceiling(1.5 * days); score -= p; penalties.Add($"Vencido {w.Title}: -{p} ({days}d)"); overdueOpen++;
                }
            }
            if (w.ActualHours > w.EstimatedHours && w.EstimatedHours > 0)
            {
                var extra = w.ActualHours - w.EstimatedHours;
                var p = (int)Math.Ceiling((double)extra * 0.5); if (p > 20) p = 20;
                score -= p; penalties.Add($"Horas extra {w.Title}: -{p} ({extra}h)"); hoursExtra += extra;
            }
        }
        if (score < 0) score = 0;
        return Result.Success(new EmployeeEffectivenessResponse(q.UserId, score, penalties, total, completedLate, overdueOpen, reopened, hoursExtra));
    }
}

public sealed record GetProjectBurnoutQuery(Guid ProjectId, Guid TenantId) : IQuery<Result<ProjectBurnoutResponse>>;
public sealed record ProjectBurnoutResponse(Guid ProjectId, int Score, string Level, IReadOnlyList<string> Factors);

public sealed class GetProjectBurnoutHandler(ProjectsDbContext db) : IQueryHandler<GetProjectBurnoutQuery, Result<ProjectBurnoutResponse>>
{
    public async Task<Result<ProjectBurnoutResponse>> HandleAsync(GetProjectBurnoutQuery q, CancellationToken ct)
    {
        var items = await db.WorkItems.AsNoTracking().Where(w => w.TenantId == q.TenantId && w.ProjectId == q.ProjectId).ToListAsync(ct);
        if (items.Count == 0) return Result.Success(new ProjectBurnoutResponse(q.ProjectId, 0, "Bajo", []));
        var now = DateTime.UtcNow;
        var overdue = items.Count(w => w.DueDate.HasValue && w.DueDate < now && w.StatusId != WorkItemStatus.Completed.Id);
        var overduePct = (double)overdue / items.Count * 100;
        var totalEst = items.Sum(w => w.EstimatedHours);
        var totalAct = items.Sum(w => w.ActualHours);
        var ratio = totalEst > 0 ? (double)totalAct / (double)totalEst : 1.0;
        var ratioScore = ratio > 1.3 ? Math.Min(30, (ratio - 1.3) * 100) : 0;
        var reopened = items.Count(w => w.ReopenedCount > 0);
        var reopenedPct = (double)reopened / items.Count * 100;
        // last week vs previous week completed
        var lastWeek = items.Count(w => w.CompletedAt.HasValue && w.CompletedAt >= now.AddDays(-7));
        var prevWeek = items.Count(w => w.CompletedAt.HasValue && w.CompletedAt >= now.AddDays(-14) && w.CompletedAt < now.AddDays(-7));
        var drop = prevWeek > 0 && lastWeek < prevWeek ? (1 - (double)lastWeek / prevWeek) * 100 : 0;
        var factors = new List<string>();
        var score = 0.0;
        score += overduePct * 0.4; if (overduePct > 0) factors.Add($"Overdue {overduePct:F0}% (40%)");
        score += ratioScore; if (ratioScore > 0) factors.Add($"Horas extra ratio {ratio:F2} (+{ratioScore:F0})");
        score += reopenedPct * 0.15; if (reopenedPct > 0) factors.Add($"Reabiertos {reopenedPct:F0}% (15%)");
        score += drop * 0.15; if (drop > 0) factors.Add($"Descenso ritmo {drop:F0}% (15%)");
        var level = score > 60 ? "Alto" : score > 40 ? "Medio" : "Bajo";
        return Result.Success(new ProjectBurnoutResponse(q.ProjectId, (int)Math.Round(score), level, factors));
    }
}
