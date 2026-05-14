namespace Ffmt.Core.Storage.S3;

public interface IS3ArchiveUploader
{
    Task UploadAsync(string key, byte[] data, CancellationToken ct = default);
    Task<byte[]?> DownloadAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
