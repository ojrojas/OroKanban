using Documents.Domain.Services;

namespace Documents.Infrastructure.Scanning;

public sealed class FakeSecurityScanProvider : ISecurityScanProvider
{
    private readonly Func<string, ScanResult> _resolver;

    public FakeSecurityScanProvider(Func<string, ScanResult>? resolver = null)
    {
        _resolver = resolver ?? (_ => new ScanResult(true, null, ScanFailureKind.Clean));
    }

    public static FakeSecurityScanProvider Clean() => new(_ => new ScanResult(true, null, ScanFailureKind.Clean));
    public static FakeSecurityScanProvider Infected() => new(_ => new ScanResult(false, "Infected", ScanFailureKind.Infected));
    public static FakeSecurityScanProvider Unavailable() => new(_ => new ScanResult(false, "ScannerUnavailable", ScanFailureKind.Unavailable));

    public Task<ScanResult> ScanAsync(Stream content, string contentHash, CancellationToken ct)
    {
        var result = _resolver(contentHash);
        return Task.FromResult(result);
    }
}
