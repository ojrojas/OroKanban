namespace Documents.Application.Common;
public static class ConcurrencyHelper { public static byte[] ParseRowVersion(string? base64) => string.IsNullOrEmpty(base64) ? Array.Empty<byte>() : Convert.FromBase64String(base64); }
