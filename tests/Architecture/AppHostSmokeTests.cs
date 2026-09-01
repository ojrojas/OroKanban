using Xunit;

namespace Architecture;

public sealed class AppHostSmokeTests
{
    private static string ReadAppHost() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "OroKanban.AppHost", "AppHost.cs"));

    // Fallback to repo-relative path when test host is not yet built
    private static string FindAppHostPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "OroKanban.AppHost", "AppHost.cs"),
            Path.Combine(Directory.GetCurrentDirectory(), "OroKanban.AppHost", "AppHost.cs"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "OroKanban.AppHost", "AppHost.cs"),
            "OroKanban.AppHost/AppHost.cs"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return File.ReadAllText(c);
            var abs = Path.GetFullPath(c);
            if (File.Exists(abs)) return File.ReadAllText(abs);
        }

        // Search from repo root upward
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "OroKanban.AppHost", "AppHost.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        return "";
    }

    [Fact]
    public void AppHost_ShouldDeclarePostgresRabbitMqRedisAndExternalIdentity()
    {
        var content = FindAppHostPath();
        Assert.False(string.IsNullOrWhiteSpace(content), "Could not locate OroKanban.AppHost/AppHost.cs");

        Assert.Contains("AddPostgres", content);
        Assert.Contains("AddRabbitMQ", content);
        Assert.Contains("AddRedis", content);
        Assert.Contains("oroidentityserver", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddProject", content);
        Assert.Contains("WithReference", content);
        Assert.Contains("Identity__Authority", content);
    }

    [Fact]
    public void Api_ShouldExposeHealthEndpoints()
    {
        // Verify Api Program.cs wires ServiceDefaults and health
        var candidates = new[]
        {
            "src/Api/Program.cs",
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Api", "Program.cs"),
        };

        string? programPath = null;
        foreach (var c in candidates)
        {
            if (File.Exists(c)) { programPath = c; break; }
            var abs = Path.GetFullPath(c);
            if (File.Exists(abs)) { programPath = abs; break; }
        }

        // Fallback search from repo root
        if (programPath is null)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "Api", "Program.cs");
                if (File.Exists(candidate)) { programPath = candidate; break; }
                dir = dir.Parent;
            }
        }

        Assert.False(programPath is null, "Could not locate src/Api/Program.cs");
        var content = File.ReadAllText(programPath!);
        Assert.Contains("AddServiceDefaults", content);
        Assert.Contains("MapDefaultEndpoints", content);
        Assert.Contains("AddOidcAuthentication", content);
    }
}
