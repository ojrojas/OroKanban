using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;
using Projects.Infrastructure.Services;

namespace Projects.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProjectsDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IDependencyCycleDetector, DependencyCycleDetector>();
        services.AddScoped<IWorkItemTransitionPolicy, WorkItemTransitionPolicy>();
        services.AddScoped<IHierarchyInspector, HierarchyInspector>();
        services.AddScoped<IAssignmentPolicy, AssignmentPolicy>();
        services.AddScoped<IProjectMembership, ProjectMembershipService>();
        services.AddScoped<IUserStateChecker, DefaultUserStateChecker>();
        return services;
    }

    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        // for AppHost wiring via connection string from config; fallback to Npgsql from Aspire
        services.AddDbContext<ProjectsDbContext>((sp, o) =>
        {
            // resolved via Aspire Npgsql connection string if available; otherwise in-mem for tests
        });
        services.AddScoped<IDependencyCycleDetector, DependencyCycleDetector>();
        services.AddScoped<IWorkItemTransitionPolicy, WorkItemTransitionPolicy>();
        services.AddScoped<IHierarchyInspector, HierarchyInspector>();
        services.AddScoped<IAssignmentPolicy, AssignmentPolicy>();
        services.AddScoped<IProjectMembership, ProjectMembershipService>();
        services.AddScoped<IUserStateChecker, DefaultUserStateChecker>();
        return services;
    }
}