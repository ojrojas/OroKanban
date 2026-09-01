using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace Notifications.Domain.ValueObjects;

public sealed class NotificationContent : ValueObject
{
    public string Title { get; }
    public string Body { get; }
    public string Link { get; }

    public NotificationContent(string title, string body, string link)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200) throw new ArgumentException("Title 1..200 required");
        if (string.IsNullOrWhiteSpace(body) || body.Length > 2000) throw new ArgumentException("Body 1..2000 required");
        if (string.IsNullOrWhiteSpace(link) || link.Length > 500) throw new ArgumentException("Link 1..500 required");
        Title = title;
        Body = body;
        Link = link;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Title;
        yield return Body;
        yield return Link;
    }
}
