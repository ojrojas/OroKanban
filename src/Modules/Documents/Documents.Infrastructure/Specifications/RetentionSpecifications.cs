using BuildingBlocks.Kernel.Domain.Specifications;
using Documents.Domain.Aggregates;
namespace Documents.Infrastructure.Specifications;
public sealed class RetentionExpiredSpec : Specification<Document>
{
    public RetentionExpiredSpec(DateTime now) { Where(d => d.RetentionRetainUntil != null && d.RetentionRetainUntil <= now && !d.RetentionLegalHold); }
}
