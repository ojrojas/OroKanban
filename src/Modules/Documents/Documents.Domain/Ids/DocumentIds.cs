using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.Ids;

public sealed record DocumentId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static DocumentId New() => new(Guid.NewGuid());
    public static DocumentId From(Guid value) => new(value);
}

public sealed record DocumentVersionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static DocumentVersionId New() => new(Guid.NewGuid());
    public static DocumentVersionId From(Guid value) => new(value);
}

public sealed record DocumentProcessingJobId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static DocumentProcessingJobId New() => new(Guid.NewGuid());
    public static DocumentProcessingJobId From(Guid value) => new(value);
}
