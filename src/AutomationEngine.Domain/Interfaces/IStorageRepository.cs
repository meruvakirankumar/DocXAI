namespace AutomationEngine.Domain.Interfaces;

public interface IStorageRepository
{
    Task<string> ReadFileContentAsync(string bucketName, string objectName, CancellationToken ct = default);
    Task<byte[]> ReadFileBytesAsync(string bucketName, string objectName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListObjectNamesAsync(string bucketName, string prefix, CancellationToken ct = default);
    Task SaveFileAsync(string bucketName, string objectPath, string content, string contentType = "text/plain", CancellationToken ct = default);
    Task SaveFileBytesAsync(string bucketName, string objectPath, byte[] content, string contentType, CancellationToken ct = default);
}
