using Amazon.S3;
using Amazon.S3.Model;
using Ffmt.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Storage.S3;

public sealed class S3ArchiveUploader : IS3ArchiveUploader, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3ArchiveUploader(IOptions<ArchiveOptions> options)
    {
        var opts = options.Value;
        _bucket = opts.Bucket;
        _client = new AmazonS3Client(
            opts.AccessKey,
            opts.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = opts.Endpoint,
                ForcePathStyle = true
            });
    }

    public async Task UploadAsync(string key, byte[] data, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = new MemoryStream(data),
            ContentType = "application/octet-stream",
            CannedACL = S3CannedACL.PublicRead
        }, ct).ConfigureAwait(false);
    }

    public async Task<byte[]?> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct).ConfigureAwait(false);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken
            }, ct).ConfigureAwait(false);

            keys.AddRange(response.S3Objects.Select(o => o.Key));
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        } while (continuationToken is not null);

        return keys;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, ct).ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();
}
