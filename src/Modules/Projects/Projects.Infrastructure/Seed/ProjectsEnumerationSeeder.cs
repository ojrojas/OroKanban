using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Seed;

public sealed class ProjectsEnumerationSeeder : IHostedService
{
    private readonly IServiceProvider _sp;
    public ProjectsEnumerationSeeder(IServiceProvider sp) => _sp = sp;

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        // Ensure schema exists; enumerations are defined via Enumeration values, no table needed for MVP
        // Seeding would insert into enumeration tables if they existed; for now ensure Projects table is queryable
        await db.Database.MigrateAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}