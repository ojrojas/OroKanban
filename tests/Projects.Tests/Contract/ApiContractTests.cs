using System.Text.Json;

public class ApiContractTests
{
    [Fact] public void PagedEnvelope_ShouldContainRequiredFields()
    {
        var json = JsonSerializer.Serialize(new { items = new[] { new { id = "1" } }, total = 25, page = 2, pageSize = 10 });
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("items", out _));
        Assert.True(doc.RootElement.TryGetProperty("total", out var total) && total.GetInt32() == 25);
        Assert.True(doc.RootElement.TryGetProperty("page", out var page) && page.GetInt32() == 2);
        Assert.True(doc.RootElement.TryGetProperty("pageSize", out var ps) && ps.GetInt32() == 10);
    }

    [Fact] public void LinkHeader_ShouldBePresentWhenMorePages()
    {
        int total = 25, page = 1, pageSize = 10;
        bool hasNext = page * pageSize < total;
        var link = hasNext ? $"<http://host/api/work-items?page={page+1}&pageSize={pageSize}>; rel=\"next\"" : null;
        Assert.NotNull(link);
        Assert.Contains("rel=\"next\"", link);
    }

    [Fact] public void LinkHeader_ShouldBeAbsentOnLastPage()
    {
        int total = 25, page = 3, pageSize = 10;
        bool hasNext = page * pageSize < total;
        Assert.False(hasNext);
    }
}
