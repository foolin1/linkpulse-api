using LinkPulse.Api.Data;

namespace LinkPulse.Api.Features.Links;

public static class ShortLinkInputValidator
{
    private const int AliasMinLength = 3;

    private static readonly HashSet<string> ReservedAliases =
        new(
            [
                "api",
                "health",
                "version",
                "openapi",
                "swagger"
            ],
            StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, string[]> ValidateCreate(
        CreateShortLinkRequest request,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        ValidateTargetUrl(
            request.TargetUrl,
            errors);

        ValidateExpiration(
            request.ExpiresAt,
            currentTime,
            errors);

        if (!string.IsNullOrWhiteSpace(
                request.CustomAlias))
        {
            ValidateAlias(
                request.CustomAlias,
                errors);
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(
        UpdateShortLinkRequest request,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        ValidateTargetUrl(
            request.TargetUrl,
            errors);

        ValidateExpiration(
            request.ExpiresAt,
            currentTime,
            errors);

        return errors;
    }

    public static string NormalizeTargetUrl(
        string targetUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);

        return new Uri(
            targetUrl.Trim(),
            UriKind.Absolute).AbsoluteUri;
    }

    public static string NormalizeAlias(
        string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        return alias.Trim().ToLowerInvariant();
    }

    private static void ValidateTargetUrl(
        string? targetUrl,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            errors["targetUrl"] =
            [
                "Target URL is required."
            ];

            return;
        }

        if (targetUrl.Length
            > EntityConstraints.TargetUrlMaxLength)
        {
            errors["targetUrl"] =
            [
                $"Target URL cannot exceed {EntityConstraints.TargetUrlMaxLength} characters."
            ];

            return;
        }

        if (!Uri.TryCreate(
                targetUrl.Trim(),
                UriKind.Absolute,
                out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || (uri.Scheme != Uri.UriSchemeHttp
                && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors["targetUrl"] =
            [
                "Target URL must be an absolute HTTP or HTTPS URL."
            ];
        }
    }

    private static void ValidateAlias(
        string alias,
        IDictionary<string, string[]> errors)
    {
        var normalizedAlias = NormalizeAlias(alias);

        if (normalizedAlias.Length < AliasMinLength
            || normalizedAlias.Length
            > EntityConstraints.ShortCodeMaxLength)
        {
            errors["customAlias"] =
            [
                $"Custom alias must contain between {AliasMinLength} and {EntityConstraints.ShortCodeMaxLength} characters."
            ];

            return;
        }

        if (!char.IsAsciiLetterOrDigit(
                normalizedAlias[0])
            || !char.IsAsciiLetterOrDigit(
                normalizedAlias[^1])
            || normalizedAlias.Any(
                character =>
                    !char.IsAsciiLetterOrDigit(character)
                    && character != '-'
                    && character != '_'))
        {
            errors["customAlias"] =
            [
                "Custom alias may contain letters, digits, hyphens and underscores, and must start and end with a letter or digit."
            ];

            return;
        }

        if (ReservedAliases.Contains(normalizedAlias))
        {
            errors["customAlias"] =
            [
                "The specified alias is reserved by the application."
            ];
        }
    }

    private static void ValidateExpiration(
        DateTimeOffset? expiresAt,
        DateTimeOffset currentTime,
        IDictionary<string, string[]> errors)
    {
        if (expiresAt.HasValue
            && expiresAt.Value <= currentTime)
        {
            errors["expiresAt"] =
            [
                "Expiration time must be in the future."
            ];
        }
    }
}