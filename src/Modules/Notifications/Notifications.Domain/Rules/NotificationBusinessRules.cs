using BuildingBlocks.Kernel.Domain.Rules;

namespace Notifications.Domain.Rules;

public sealed class DedupeKeyRequiredRule(Guid sourceEventId, Guid recipientId) : IBusinessRule
{
    public bool IsBroken() => sourceEventId == Guid.Empty || recipientId == Guid.Empty;
    public string Message => "DedupeKey requires SourceEventId and RecipientId";
}

public sealed class TitleRequiredRule(string title) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(title) || title.Length > 200;
    public string Message => "Title 1..200 required";
}

public sealed class LinkRequiredRule(string link) : IBusinessRule
{
    public bool IsBroken() => string.IsNullOrWhiteSpace(link) || link.Length > 500;
    public string Message => "Link 1..500 required";
}

public sealed class ContentSafetyRule(string title, string body) : IBusinessRule
{
    // Defense: title/body must not contain raw payload markers; allowlist is enforced by policy but rule guards obvious leaks
    private static readonly string[] ForbiddenMarkers = ["<binary>", "PAYLOAD"];
    public bool IsBroken() => ForbiddenMarkers.Any(m => title.Contains(m) || body.Contains(m));
    public string Message => "Content contains forbidden payload marker";
}
