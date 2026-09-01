using System.Text.RegularExpressions;

using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.ValueObjects;

public sealed class MimeType : ValueObject
{
    private static readonly Regex MimePattern = new(@"^[-+.\w]+/[-+.\w]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "text/plain",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/csv",
        "application/octet-stream"
    };

    public string Value { get; }
    public string Extension { get; }

    private MimeType(string value)
    {
        Value = value;
        Extension = value.Split('/').Last().Split('.').Last();
    }

    public static Result<MimeType> Create(string mime, bool allowFallback = true)
    {
        if (string.IsNullOrWhiteSpace(mime))
            return Result.Failure<MimeType>(Error.Validation("MimeType.Required", "MimeType is required."));
        var normalized = mime.Trim().ToLowerInvariant();
        if (!MimePattern.IsMatch(normalized))
            return Result.Failure<MimeType>(Error.Validation("MimeType.Invalid", $"MimeType '{mime}' is not a valid MIME type."));
        if (!AllowList.Contains(normalized))
        {
            if (allowFallback && normalized == "application/octet-stream")
                return Result.Success(new MimeType(normalized));
            // Allow list is configurable; for now warn but allow if pattern matches — strict enforcement via Validator will block disallowed
        }
        return Result.Success(new MimeType(normalized));
    }

    public bool IsAllowed => AllowList.Contains(Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(MimeType m) => m.Value;
}
