using System.Text.Json;
using Audit.Domain.Services;
using Audit.Domain.ValueObjects;

namespace Audit.Infrastructure.Services;

public sealed class AuditMaskingPolicy : IAuditMaskingPolicy
{
    private readonly string[] _maskedFields;
    public AuditMaskingPolicy(string[]? maskedFields = null)
    {
        _maskedFields = maskedFields ?? new[] { "ApiKey", "Password", "Secret", "ConnectionString", "Token", "CreditCard", "PrivateKey" };
    }

    public BeforeAfterSnapshot Mask(BeforeAfterSnapshot raw)
    {
        string MaskJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream);
                MaskElement(doc.RootElement, writer);
                writer.Flush();
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
            catch { return json; }
        }
        void MaskElement(JsonElement element, Utf8JsonWriter writer)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var prop in element.EnumerateObject())
                    {
                        writer.WritePropertyName(prop.Name);
                        if (Array.Exists(_maskedFields, f => string.Equals(f, prop.Name, StringComparison.OrdinalIgnoreCase)))
                            writer.WriteStringValue("***");
                        else
                            MaskElement(prop.Value, writer);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray()) MaskElement(item, writer);
                    writer.WriteEndArray();
                    break;
                default:
                    element.WriteTo(writer);
                    break;
            }
        }
        return new BeforeAfterSnapshot(MaskJson(raw.BeforeJson), MaskJson(raw.AfterJson));
    }
}
