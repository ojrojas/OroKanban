using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.ValueObjects;

public sealed class Provenance : ValueObject
{
    public string Source { get; }
    public string OriginalFilename { get; }
    public Guid UploadedBy { get; }
    public DateTime UploadedAt { get; }

    public Provenance(string source, string originalFilename, Guid uploadedBy, DateTime uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 200)
            throw new ArgumentException("Source must be 1..200 chars", nameof(source));
        if (string.IsNullOrWhiteSpace(originalFilename) || originalFilename.Length > 300)
            throw new ArgumentException("OriginalFilename must be 1..300 chars", nameof(originalFilename));
        Source = source;
        OriginalFilename = originalFilename;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt.Kind == DateTimeKind.Utc ? uploadedAt : uploadedAt.ToUniversalTime();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Source;
        yield return OriginalFilename;
        yield return UploadedBy;
        yield return UploadedAt;
    }
}
