namespace Audit.Application.Common;
public static class AuditPaginationHelper
{
    public static string BuildLinkHeader(int page, int pageSize, int totalCount, string baseUrl)
    {
        if ((page * pageSize) >= totalCount) return string.Empty;
        return $"<{baseUrl}?page={page+1}&pageSize={pageSize}>; rel=\"next\"";
    }
}
