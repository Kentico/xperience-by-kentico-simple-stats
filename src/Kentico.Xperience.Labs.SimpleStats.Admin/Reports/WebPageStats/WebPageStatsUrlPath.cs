namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;

/// <summary>
/// Normalizes a web page URL to the path form submission activities are matched by.
/// Mirrors the path expression in <see cref="WebPageStatsRepository.Query"/>, which compares with and without one trailing slash.
/// </summary>
internal static class WebPageStatsUrlPath
{
    /// <summary>
    /// Returns the path of <paramref name="url"/> without the scheme and host, the query string, the fragment and trailing slashes,
    /// always starting with <c>/</c>, or an empty string for the site root.
    /// Accepts virtual paths (<c>~/articles</c>), relative paths (<c>/articles</c>) and absolute URLs (<c>https://host/articles</c>).
    /// Returns <c>null</c> for an empty value. Letter case is kept: the database compares case-insensitively.
    /// </summary>
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string path = url.Trim();

        int schemeEnd = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            int pathStart = path.IndexOf('/', schemeEnd + 3);
            path = pathStart >= 0 ? path[pathStart..] : string.Empty;
        }
        else if (path.StartsWith('~'))
        {
            path = path[1..];
        }

        int cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        path = path.TrimEnd('/');

        return path.Length == 0 || path.StartsWith('/') ? path : "/" + path;
    }

    /// <summary>
    /// Returns the host of a configured domain (for example <c>fr.example.com:8080</c>), lowercase, with the port kept and a
    /// scheme or path removed, or <c>null</c> for an empty value. Mirrors the host expression in <see cref="WebPageStatsRepository.Query"/>.
    /// </summary>
    public static string? NormalizeHost(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        string host = domain.Trim();

        int schemeEnd = host.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            host = host[(schemeEnd + 3)..];
        }

        int pathStart = host.IndexOfAny(['/', '?', '#']);
        if (pathStart >= 0)
        {
            host = host[..pathStart];
        }

        return host.Length == 0 ? null : host.ToLowerInvariant();
    }
}
