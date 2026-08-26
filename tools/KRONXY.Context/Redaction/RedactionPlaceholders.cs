namespace Kronxy.Context.Redaction;

internal static class RedactionPlaceholders
{
    private static readonly HashSet<string> Allowed = Enum.GetValues<RedactionCategory>()
        .Select(For).ToHashSet(StringComparer.Ordinal);

    public static string For(RedactionCategory category) => category switch
    {
        RedactionCategory.Password => "__KRONXY_REDACTED_PASSWORD__",
        RedactionCategory.ApiKey => "__KRONXY_REDACTED_API_KEY__",
        RedactionCategory.PrivateKey => "__KRONXY_REDACTED_PRIVATE_KEY__",
        RedactionCategory.ConnectionString => "__KRONXY_REDACTED_CONNECTION_STRING__",
        RedactionCategory.AuthorizationHeader => "__KRONXY_REDACTED_AUTHORIZATION_HEADER__",
        RedactionCategory.UrlCredential => "__KRONXY_REDACTED_URL_CREDENTIAL__",
        RedactionCategory.SignedUrl => "__KRONXY_REDACTED_SIGNED_URL_VALUE__",
        RedactionCategory.CloudCredential => "__KRONXY_REDACTED_CLOUD_CREDENTIAL__",
        RedactionCategory.CertificateSecret => "__KRONXY_REDACTED_CERTIFICATE_SECRET__",
        RedactionCategory.GenericSecret => "__KRONXY_REDACTED_SECRET__",
        _ => "__KRONXY_REDACTED_TOKEN__"
    };

    public static bool IsValid(string value) => Allowed.Contains(value);
}
