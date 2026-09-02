using Amazon.S3;
using Amazon.S3.Model;
using KotoDibo.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace KotoDibo.Infrastructure.Storage;

public class R2StorageService : IFileStorageService
{
    private readonly IAmazonS3 _client;
    private readonly R2Settings _settings;

    public R2StorageService(IAmazonS3 client, IOptions<R2Settings> settings)
    {
        _client = client;
        _settings = settings.Value;
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        };

        await _client.PutObjectAsync(request, cancellationToken);
        return GetPublicUrl(key);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_settings.BucketName, key, cancellationToken);
    }

    public string GetPublicUrl(string key) => $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
}
