using System.Text.RegularExpressions;

using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.ValueObjects;

public sealed class MetadataSnapshot : ValueObject
{
    private static readonly Regex TagPattern = new(@"^[a-z0-9_-]+$", RegexOptions.Compiled);

    public string? Author { get; }
    public string? Department { get; }
    public string? ProjectText { get; }
    public IReadOnlySet<string> Tags { get; }
    public string? DocumentType { get; }
    public DateTime? EffectiveDate { get; }
    public DateTime? ExpirationDate { get; }
    public string? Source { get; }
    public string? Confidentiality { get; }
    public RetentionPolicy RetentionPolicy { get; }
    public IReadOnlyDictionary<string, string> CustomMetadata { get; }

    public MetadataSnapshot(
        string? author,
        string? department,
        string? projectText,
        IReadOnlySet<string>? tags,
        string? documentType,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        string? source,
        string? confidentiality,
        RetentionPolicy? retentionPolicy,
        IReadOnlyDictionary<string, string>? customMetadata)
    {
        if (author is not null && (author.Length < 1 || author.Length > 200))
            throw new ArgumentException("Author must be 1..200 chars", nameof(author));
        if (department is not null && (department.Length < 1 || department.Length > 200))
            throw new ArgumentException("Department must be 1..200 chars", nameof(department));
        if (projectText is not null && (projectText.Length < 1 || projectText.Length > 200))
            throw new ArgumentException("ProjectText must be 1..200 chars", nameof(projectText));
        if (documentType is not null && (documentType.Length < 1 || documentType.Length > 100))
            throw new ArgumentException("DocumentType must be 1..100 chars", nameof(documentType));
        if (source is not null && (source.Length < 1 || source.Length > 200))
            throw new ArgumentException("Source must be 1..200 chars", nameof(source));
        if (confidentiality is not null && (confidentiality.Length < 1 || confidentiality.Length > 100))
            throw new ArgumentException("Confidentiality must be 1..100 chars", nameof(confidentiality));
        if (effectiveDate is not null && expirationDate is not null && effectiveDate > expirationDate)
            throw new ArgumentException("EffectiveDate must be <= ExpirationDate");
        if (tags is not null)
        {
            if (tags.Count > 50) throw new ArgumentException("Tags must be <= 50", nameof(tags));
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || tag.Length > 50 || !TagPattern.IsMatch(tag.ToLowerInvariant()))
                    throw new ArgumentException($"Tag '{tag}' invalid: must match ^[a-z0-9_-]+$ 1..50", nameof(tags));
            }
        }
        if (customMetadata is not null)
        {
            if (customMetadata.Count > 50) throw new ArgumentException("CustomMetadata must be <= 50 entries", nameof(customMetadata));
            foreach (var kv in customMetadata)
            {
                if (kv.Key.Length > 64) throw new ArgumentException($"CustomMetadata key '{kv.Key}' exceeds 64 chars", nameof(customMetadata));
                if (kv.Value.Length > 2048) throw new ArgumentException($"CustomMetadata value for '{kv.Key}' exceeds 2KB", nameof(customMetadata));
            }
        }

        Author = author;
        Department = department;
        ProjectText = projectText;
        Tags = tags is not null ? new HashSet<string>(tags.Select(t => t.Trim().ToLowerInvariant())) : new HashSet<string>();
        DocumentType = documentType;
        EffectiveDate = effectiveDate;
        ExpirationDate = expirationDate;
        Source = source;
        Confidentiality = confidentiality;
        RetentionPolicy = retentionPolicy ?? RetentionPolicy.None;
        CustomMetadata = customMetadata is not null ? new Dictionary<string, string>(customMetadata) : new Dictionary<string, string>();
    }

    public static MetadataSnapshot Empty => new(null, null, null, null, null, null, null, null, null, null, null);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Author;
        yield return Department;
        yield return ProjectText;
        // Order-independent for Tags
        foreach (var tag in Tags.OrderBy(t => t))
            yield return tag;
        yield return DocumentType;
        yield return EffectiveDate;
        yield return ExpirationDate;
        yield return Source;
        yield return Confidentiality;
        yield return RetentionPolicy;
        // Order-independent for CustomMetadata
        foreach (var kv in CustomMetadata.OrderBy(kv => kv.Key))
        {
            yield return kv.Key;
            yield return kv.Value;
        }
    }
}
