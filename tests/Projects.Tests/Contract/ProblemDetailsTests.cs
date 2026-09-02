using System.Text.Json;

public class ProblemDetailsTests
{
    [Fact] public void InvalidFilter_ShouldReturn400ProblemDetailsShape()
    {
        var problem = new { type = "https://httpstatuses.io/400", title = "Validation failed", detail = "filter 'unknownField' is invalid", status = 400, code = "Validation.FilterUnknown" };
        var json = JsonSerializer.Serialize(problem);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Validation.FilterUnknown", doc.RootElement.GetProperty("code").GetString());
        Assert.True(doc.RootElement.TryGetProperty("title", out _));
        Assert.True(doc.RootElement.TryGetProperty("detail", out _));
    }

    [Fact] public void AllProblemDetails_ShouldHaveTypeTitleDetailStatusCode()
    {
        var samples = new[] {
            new { type = "x", title = "t", detail = "d", status = 400, code = "Validation.FilterUnknown" },
            new { type = "x", title = "t", detail = "d", status = 403, code = "Auth.Forbidden" },
            new { type = "x", title = "t", detail = "d", status = 404, code = "NotFound" }
        };
        foreach (var s in samples)
        {
            var doc = JsonDocument.Parse(JsonSerializer.Serialize(s));
            foreach (var prop in new[] { "type", "title", "detail", "status", "code" })
                Assert.True(doc.RootElement.TryGetProperty(prop, out _));
        }
    }
}
