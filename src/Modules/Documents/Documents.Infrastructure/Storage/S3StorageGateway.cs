using BuildingBlocks.Kernel.Domain.Results;

using Documents.Domain.Services;

namespace Documents.Infrastructure.Storage;

public sealed class S3StorageGateway : IStorageGateway
{
    private readonly Dictionary<string, byte[]> _store = new();

    public Task<Result<BlobRef>> PutAsync(Stream bytes, string contentHash, string mimeType, CancellationToken ct)
    {
        // Hash verification: re-hash bytes and compare
        using var ms = new MemoryStream();
        bytes.CopyTo(ms);
        var data = ms.ToArray();
        var computed = Domain.ValueObjects.ContentHash.FromBytes(data).Value;
        if (!string.Equals(computed, contentHash, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Result.Failure<BlobRef>(Error.Failure("Storage.HashMismatch", $"Hash mismatch: expected {contentHash}, computed {computed}")));

        var key = $"sha256/{contentHash}.{mimeType.Split('/').Last()}";
        if (!_store.ContainsKey(key))
            _store[key] = data;
        return Task.FromResult(Result.Success(new BlobRef(contentHash, key, data.Length)));
    }

    public Task<Result<Stream>> GetAsync(string contentHash, bool isSafe, CancellationToken ct)
    {
        if (!isSafe)
            return Task.FromResult(Result.Failure<Stream>(Error.Forbidden("Storage.NotSafe", "Document not marked as safe.")));
        var key = _store.Keys.FirstOrDefault(k => k.Contains(contentHash));
        if (key is null)
            return Task.FromResult(Result.Failure<Stream>(Error.NotFound("Storage.NotFound", "Blob not found.")));
        Stream s = new MemoryStream(_store[key]);
        return Task.FromResult(Result.Success(s));
    }

    public Task<bool> ExistsAsync(string contentHash, CancellationToken ct)
        => Task.FromResult(_store.Keys.Any(k => k.Contains(contentHash)));

    public string CreatePresignedUrl(string contentHash, bool isSafe, TimeSpan ttl)
    {
        if (!isSafe) throw new UnauthorizedAccessException("NotSafe");
        return $"https://s3.local/{contentHash}?expires={ttl.TotalSeconds}";
    }
}
