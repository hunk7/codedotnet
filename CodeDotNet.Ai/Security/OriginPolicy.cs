namespace CodeDotNet.Ai.Security;

public static class OriginPolicy
{
    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "[::1]", "::1",
    };

    /// <summary>
    /// No Origin header (non-browser client): allowed, but the bearer token is still required.
    /// With an Origin: exact match against the configured list, or any loopback http(s) origin.
    /// </summary>
    public static bool IsOriginAllowed(string? origin, IReadOnlyCollection<string> allowedOrigins)
    {
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && LoopbackHosts.Contains(uri.Host);
    }

    /// <summary>Blocks DNS-rebinding: the Host header must be a loopback name.</summary>
    public static bool IsHostAllowed(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        var name = host;
        if (host.StartsWith('['))
        {
            var end = host.IndexOf(']');
            name = end > 0 ? host[..(end + 1)] : host;
        }
        else
        {
            var colon = host.LastIndexOf(':');
            if (colon > 0)
            {
                name = host[..colon];
            }
        }

        return LoopbackHosts.Contains(name);
    }
}
