namespace Audit.Infrastructure.Configuration;

public sealed class AuditOptions
{
    public const string SectionName = "Audit";
    public string[] MaskedFields { get; set; } = new[] { "ApiKey", "Password", "Secret", "ConnectionString", "Token", "CreditCard", "PrivateKey" };
    public bool HashChainingEnabled { get; set; } = false;
    public int RetentionDays { get; set; } = 2555; // 7 years
}
