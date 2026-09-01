using AiProcessing.Domain.Enumerations;
using Xunit;

namespace AiProcessing.Tests.Unit;

public sealed class OperationTypeCatalogTests
{
    [Fact]
    public void All12_FromId_RoundTrip()
    {
        var all = OperationType.GetAll();
        Assert.Equal(12, all.Count);
        foreach (var op in all)
        {
            var fromId = OperationType.FromId(op.Id);
            Assert.Equal(op.Name, fromId.Name);
        }
    }
}
