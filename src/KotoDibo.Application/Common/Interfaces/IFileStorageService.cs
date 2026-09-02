namespace KotoDibo.Application.Common.Interfaces;

// CDN-backed object storage (Cloudflare R2 in production, via an S3-compatible client). Kept
// storage-agnostic at this layer the same way IRepository<T> hides Mongo — Application code asks
// for a key to be stored and a public URL back, never an S3/R2-specific type.
public interface IFileStorageService
{
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    string GetPublicUrl(string key);
}
