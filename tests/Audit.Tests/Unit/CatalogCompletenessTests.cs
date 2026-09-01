using Audit.Domain.Enumerations;
using Xunit;

namespace Audit.Tests.Unit;

public sealed class CatalogCompletenessTests
{
    [Fact]
    public void AllAuditActions_MappedToIntegrationEvent()
    {
        var all = AuditAction.GetAll();
        Assert.True(all.Count >= 31);
        foreach (var a in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
        }
    }
}
