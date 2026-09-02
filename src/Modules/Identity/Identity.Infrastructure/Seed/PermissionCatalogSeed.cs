namespace Identity.Infrastructure.Seed;

public static class PermissionCatalogSeed
{
    public static readonly IReadOnlyList<(string Code, string Description, string Category)> Permissions =
    [
        ("project.read", "Read project", "project"),
        ("project.create", "Create project", "project"),
        ("project.update", "Update project", "project"),
        ("project.delete", "Delete project", "project"),
        ("workitem.read", "Read work item", "workitem"),
        ("workitem.create", "Create work item", "workitem"),
        ("workitem.assign", "Assign work item", "workitem"),
        ("workitem.update", "Update work item", "workitem"),
        ("workitem.complete", "Complete work item", "workitem"),
        ("document.read", "Read document", "document"),
        ("document.upload", "Upload document", "document"),
        ("document.classify", "Classify document", "document"),
        ("document.version", "Version document", "document"),
        ("document.approve", "Approve document", "document"),
        ("ai.execute", "Execute AI operation", "ai"),
        ("ai.review", "Review AI result", "ai"),
        ("ai.approve", "Approve AI result", "ai"),
        ("audit.read", "Read audit", "audit"),
        ("organization.manage", "Manage organization", "organization"),
    ];

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>
    {
        ["RootManager"] = Permissions.Select(p => p.Code).ToArray(),
        ["Manager"] = ["project.read", "project.create", "project.update", "workitem.read", "workitem.create", "workitem.assign", "workitem.update", "workitem.complete", "document.read", "document.upload", "organization.manage"],
        ["Supervisor"] = ["project.read", "workitem.read", "workitem.create", "workitem.update", "document.read"],
        ["Contributor"] = ["project.read", "workitem.read", "workitem.create", "workitem.update", "document.read", "document.upload"],
        ["Reviewer"] = ["project.read", "workitem.read", "document.read", "document.approve", "ai.review"],
        ["Auditor"] = ["audit.read", "project.read", "workitem.read", "document.read"],
        ["DocumentManager"] = ["document.read", "document.upload", "document.classify", "document.version", "document.approve"],
        ["ProjectManager"] = ["project.read", "project.create", "project.update", "workitem.read", "workitem.create", "workitem.assign"],
        ["AIReviewer"] = ["ai.execute", "ai.review", "ai.approve", "document.read"],
        ["Administrator"] = Permissions.Select(p => p.Code).ToArray(),
    };
}