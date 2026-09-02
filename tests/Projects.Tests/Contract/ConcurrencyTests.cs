using System.Text.Json;

public class ConcurrencyTests
{
    [Fact] public void StaleVersion_ShouldProduceProblemDetailsWithCurrentVersion()
    {
        var problem = new { type = "https://httpstatuses.io/409", title = "Concurrency conflict", detail = "Version 4 is stale, current is 5", status = 409, code = "Concurrency.StaleVersion", currentVersion = "5" };
        var json = JsonSerializer.Serialize(problem);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(409, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Concurrency.StaleVersion", doc.RootElement.GetProperty("code").GetString());
        Assert.True(doc.RootElement.TryGetProperty("currentVersion", out var cv) && cv.GetString() == "5");
        Assert.Contains("stale", doc.RootElement.GetProperty("detail").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void ProblemDetails_ShouldContainRequiredFieldsFor409()
    {
        var json = JsonSerializer.Serialize(new { type = "x", title = "t", detail = "d", status = 409, code = "Concurrency.StaleVersion" });
        var doc = JsonDocument.Parse(json);
        foreach (var prop in new[] { "type", "title", "detail", "status", "code" })
            Assert.True(doc.RootElement.TryGetProperty(prop, out _), $"missing {prop}");
    }
}
