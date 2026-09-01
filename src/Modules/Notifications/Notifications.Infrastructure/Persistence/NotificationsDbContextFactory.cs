using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new NotificationsDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__orokanban_notifications")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}
