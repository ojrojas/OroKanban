using Audit.Domain.Aggregates;
using Xunit;

namespace Audit.Tests.Unit;

public sealed class AuditEntryIsImmutableTests
{
    [Fact]
    public void AuditEntry_HasZeroPublicSetters()
    {
        var setters = typeof(AuditEntry).GetProperties().Count(p => p.SetMethod?.IsPublic == true);
        Assert.Equal(0, setters);
    }
}
