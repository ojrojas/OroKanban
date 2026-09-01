using System.ComponentModel.DataAnnotations;

namespace Api.Configuration;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    [Required(ErrorMessage = "Identity__Authority is required but was not configured")]
    public string Authority { get; set; } = default!;

    [Required(ErrorMessage = "Identity__Audience is required but was not configured")]
    public string Audience { get; set; } = default!;

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Scopes { get; set; } = "openid profile email offline_access";
}
