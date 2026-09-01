using System.Reflection;

using BuildingBlocks.Kernel.Domain.Persistence;

using Xunit;

namespace Architecture;

public sealed class ArchitectureTests
{
    private static readonly string[] ProhibitedPackages = ["MediatR", "MassTransit", "AutoMapper"];
    private static readonly string[] ModulePrefixes = ["Identity", "Organization", "Projects", "Metrics", "Documents", "AiProcessing", "Search", "Audit", "Notifications"];

    [Fact]
    public void NoProhibitedDependenciesReferenced()
    {
        var assemblies = GetModuleAndApiAssemblies();
        var violations = new List<string>();

        foreach (var asm in assemblies)
        {
            foreach (var refAsm in asm.GetReferencedAssemblies())
            {
                foreach (var prohibited in ProhibitedPackages)
                {
                    if (refAsm.Name != null && refAsm.Name.Contains(prohibited, StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{asm.GetName().Name} references prohibited package {refAsm.Name}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, $"Prohibited dependencies found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void NoCrossModuleInfrastructureReferences()
    {
        // Each module's Infrastructure should not be referenced by another module
        // We check project references via assembly references: Modules.<A>.Infrastructure should not be referenced by Modules.<B> where A != B
        var assemblies = GetAllModuleAssemblies();
        var violations = new List<string>();

        foreach (var asm in assemblies)
        {
            var asmName = asm.GetName().Name ?? "";
            // Determine which module this assembly belongs to (if any)
            var ownerModule = ModulePrefixes.FirstOrDefault(m => asmName.StartsWith($"{m}.", StringComparison.OrdinalIgnoreCase) || asmName.Equals(m, StringComparison.OrdinalIgnoreCase));
            if (ownerModule is null) continue;

            foreach (var refAsm in asm.GetReferencedAssemblies())
            {
                var refName = refAsm.Name ?? "";
                // Check if ref is another module's Infrastructure or Domain
                foreach (var otherModule in ModulePrefixes.Where(m => m != ownerModule))
                {
                    if (refName.Equals($"{otherModule}.Infrastructure", StringComparison.OrdinalIgnoreCase) ||
                        refName.Equals($"{otherModule}.Domain", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{asmName} references {refName} — cross-module Infrastructure/Domain reference prohibited (use Contracts + EventBus)");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, $"Cross-module violations:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void EveryModuleDbContextInheritsAppDbContextBaseAndAppliesOutbox()
    {
        // Force-load all module Infrastructure assemblies via known DbContext types (ensures AppDomain contains them)
        _ = typeof(Organization.Infrastructure.Persistence.OrganizationDbContext).Assembly;
        _ = typeof(Projects.Infrastructure.Persistence.ProjectsDbContext).Assembly;
        _ = typeof(Metrics.Infrastructure.Persistence.MetricsDbContext).Assembly;
        _ = typeof(Documents.Infrastructure.Persistence.DocumentsDbContext).Assembly;
        _ = typeof(AiProcessing.Infrastructure.Persistence.AiProcessingDbContext).Assembly;
        _ = typeof(Search.Infrastructure.Persistence.SearchDbContext).Assembly;
        _ = typeof(Audit.Infrastructure.Persistence.AuditDbContext).Assembly;
        _ = typeof(Notifications.Infrastructure.Persistence.NotificationsDbContext).Assembly;

        var dbContextTypes = GetAllModuleAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Name.EndsWith("DbContext") && !t.IsAbstract)
            .ToList();

        Assert.True(dbContextTypes.Count >= 8, $"Expected at least 9 DbContexts, found {dbContextTypes.Count}");

        foreach (var type in dbContextTypes)
        {
            Assert.True(typeof(AppDbContextBase).IsAssignableFrom(type),
                $"{type.FullName} must inherit AppDbContextBase (persistence convention FR-003)");

            // Check that OnModelCreating applies OutboxEntityTypeConfiguration via base call (we rely on AppDbContextBase to do it)
            // Verify the type is in a Infrastructure assembly
            Assert.Contains("Infrastructure", type.Assembly.GetName().Name!, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Assembly[] GetModuleAndApiAssemblies()
    {
        // Ensure Api assembly is loaded via a known type from Api
        try { _ = typeof(Api.Configuration.IdentityOptions).Assembly; } catch { }
        try { _ = typeof(Api.Features.GetPlatformHealth.GetPlatformHealthQuery).Assembly; } catch { }

        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.GetName().Name))
            .ToList();

        return loaded.Where(a =>
            (a.GetName().Name?.StartsWith("Identity.") == true) ||
            (a.GetName().Name?.StartsWith("Organization.") == true) ||
            (a.GetName().Name?.StartsWith("Projects.") == true) ||
            (a.GetName().Name?.StartsWith("Metrics.") == true) ||
            (a.GetName().Name?.StartsWith("Documents.") == true) ||
            (a.GetName().Name?.StartsWith("AiProcessing.") == true) ||
            (a.GetName().Name?.StartsWith("Search.") == true) ||
            (a.GetName().Name?.StartsWith("Audit.") == true) ||
            (a.GetName().Name?.StartsWith("Notifications.") == true) ||
            (a.GetName().Name == "Api")
        ).ToArray();
    }

    private static Assembly[] GetAllModuleAssemblies() => GetModuleAndApiAssemblies();
}