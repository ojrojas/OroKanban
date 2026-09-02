namespace Api.Configuration;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    // Authority/Audience pueden venir de Identity:* o Oidc:* (patrón EduCore + docs). Validación cruzada en Program.cs.
    public string Authority { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Scopes { get; set; } = "openid profile email offline_access";
}