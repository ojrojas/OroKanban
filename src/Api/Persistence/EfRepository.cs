using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Api.Persistence;

/// <summary>
/// Generic EF Core repository bound to a specific AppDbContextBase subtype.
/// Fixes: BuildingBlocks.Kernel.Infrastructure EfRepository was never implemented,
/// so IRepository&lt;Notification&gt; etc. could not be resolved (AggregateException).
/// </summary>
public sealed class EfRepository<TContext, TAggregate, TId> : IRepository<TAggregate, TId>
    where TContext : DbContext
    where TAggregate : class, IAggregateRoot
    where TId : notnull
{
    private readonly TContext _db;
    private DbSet<TAggregate> Set => _db.Set<TAggregate>();

    public EfRepository(TContext db) => _db = db;

    public async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => await Set.FindAsync(new object?[] { id }, cancellationToken);

    public Task<TAggregate?> FirstOrDefaultAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => Apply(specification).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TAggregate>> ListAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => await Apply(specification).ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => Apply(specification).AnyAsync(cancellationToken);

    public Task<int> CountAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default)
        => Apply(specification).CountAsync(cancellationToken);

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
        => await Set.AddAsync(aggregate, cancellationToken);

    public void Update(TAggregate aggregate) => Set.Update(aggregate);

    public void Remove(TAggregate aggregate) => Set.Remove(aggregate);

    private IQueryable<TAggregate> Apply(ISpecification<TAggregate> spec)
    {
        IQueryable<TAggregate> q = Set;

        if (spec.Criteria is not null)
            q = q.Where(spec.Criteria);

        foreach (var inc in spec.Includes)
            q = q.Include(inc);

        if (spec.OrderBy is not null)
            q = q.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            q = q.OrderByDescending(spec.OrderByDescending);

        if (spec.Skip.HasValue)
            q = q.Skip(spec.Skip.Value);
        if (spec.Take.HasValue)
            q = q.Take(spec.Take.Value);

        if (spec.AsNoTracking)
            q = q.AsNoTracking();

        return q;
    }
}


