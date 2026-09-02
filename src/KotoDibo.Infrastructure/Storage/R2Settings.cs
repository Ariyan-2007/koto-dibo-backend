namespace KotoDibo.Infrastructure.Storage;

// Cloudflare R2, accessed through its S3-compatible API. AccessKeyId/SecretAccessKey are R2 API
// token credentials (Cloudflare dashboard -> R2 -> Manage API tokens) — NOT the general Cloudflare
// "Api-token" and not the Account ID. Keep these out of appsettings*.json (which are committed);
// supply them via environment variables (R2__AccessKeyId / R2__SecretAccessKey) or user-secrets.
public class R2Settings
{
    public const string SectionName = "R2";

    // https://<account-id>.r2.cloudflarestorage.com — the S3 API endpoint, not the public bucket URL.
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;

    // Public r2.dev (or custom domain) base the bucket is served from, e.g. "https://pub-xxxx.r2.dev".
    // No trailing slash.
    public string PublicBaseUrl { get; set; } = string.Empty;
}
