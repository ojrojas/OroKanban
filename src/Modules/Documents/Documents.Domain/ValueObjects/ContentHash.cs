using System.Security.Cryptography;
using System.Text.RegularExpressions;

using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Documents.Domain.ValueObjects;

public sealed class ContentHash : ValueObject
{
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    public string Value { get; }

    private ContentHash(string value) => Value = value;

    public static Result<ContentHash> Create(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return Result.Failure<ContentHash>(Error.Validation("ContentHash.Required", "ContentHash is required."));
        var normalized = hash.Trim().ToLowerInvariant();
        if (!Hex64.IsMatch(normalized))
            return Result.Failure<ContentHash>(Error.Validation("ContentHash.Invalid", "ContentHash must be 64 lowercase hex characters (SHA-256)."));
        return Result.Success(new ContentHash(normalized));
    }

    public static ContentHash FromBytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return new ContentHash(hex);
    }

    public static ContentHash FromBytes(ReadOnlySpan<byte> bytes)
    {
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return new ContentHash(hex);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ContentHash hash) => hash.Value;
}
