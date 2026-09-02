using BuildingBlocks.Kernel.Domain.Repositories;

namespace Api.Persistence;

/// <summary>
/// Composite UnitOfWork that flushes all tracked AppDbContextBase instances.
/// Handlers inject non-generic IUnitOfWork; the concrete DbContext that was mutated
/// (usually NotificationsDbContext) gets its SaveChangesAsync invoked. Other contexts
/// are no-ops (0 changes). This satisfies BuildingBlocks draft where AddUnitOfWork&lt;T&gt;
/// was never wired and caused AggregateException on NotificationDispatcher.
/// </summary>
public sealed class CompositeUnitOfWork : IUnitOfWork
{
    private readonly IServiceProvider _sp;

    public CompositeUnitOfWork(IServiceProvider sp) => _sp = sp;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Resolve every registered AppDbContextBase subtype. Those not registered return null.
        var contexts = new[]
        {
            _sp.GetService(typeof(Organization.Infrastructure.Persistence.OrganizationDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(Projects.Infrastructure.Persistence.ProjectsDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(Documents.Infrastructure.Persistence.DocumentsDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(AiProcessing.Infrastructure.Persistence.AiProcessingDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(Audit.Infrastructure.Persistence.AuditDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(Notifications.Infrastructure.Persistence.NotificationsDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
            _sp.GetService(typeof(Metrics.Infrastructure.Persistence.MetricsDbContext)) as BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase,
        }.Where(c => c is not null).Cast<BuildingBlocks.Kernel.Domain.Persistence.AppDbContextBase>();

        int total = 0;
        foreach (var ctx in contexts)
            total += await ctx.SaveChangesAsync(cancellationToken);

        return total;
    }
}
