using System.Text;
using AutomationEngine.Domain.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;

namespace AutomationEngine.Infrastructure.GoogleCloud.Storage;

public sealed class GcpCloudStorageService : IStorageRepository
{
    private readonly StorageClient _storageClient;
    private readonly ILogger<GcpCloudStorageService> _logger;

    public GcpCloudStorageService(GoogleCredential credential, ILogger<GcpCloudStorageService> logger)
    {
        // Credential loaded from Secret Manager (or ADC in local dev)
        _storageClient = StorageClient.Create(credential);
        _logger = logger;
    }

    public async Task<string> ReadFileContentAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        _logger.LogInformation("Downloading gs://{Bucket}/{Object}", bucketName, objectName);

        using var ms = new MemoryStream();
        await _storageClient.DownloadObjectAsync(bucketName, objectName, ms, cancellationToken: ct);
        ms.Position = 0;
        var content = Encoding.UTF8.GetString(ms.ToArray());

        _logger.LogInformation("Downloaded {Bytes} bytes from gs://{Bucket}/{Object}", content.Length, bucketName, objectName);
        return content;
    }

    public async Task<byte[]> ReadFileBytesAsync(string bucketName, string objectName, CancellationToken ct = default)
    {
        _logger.LogInformation("Downloading bytes gs://{Bucket}/{Object}", bucketName, objectName);

        using var ms = new MemoryStream();
        await _storageClient.DownloadObjectAsync(bucketName, objectName, ms, cancellationToken: ct);
        var bytes = ms.ToArray();

        _logger.LogInformation("Downloaded {Bytes} bytes from gs://{Bucket}/{Object}", bytes.Length, bucketName, objectName);
        return bytes;
    }

    public async Task<IReadOnlyList<string>> ListObjectNamesAsync(string bucketName, string prefix, CancellationToken ct = default)
    {
        var names = new List<string>();
        var listOptions = new Google.Cloud.Storage.V1.ListObjectsOptions { Delimiter = null };
        await foreach (var obj in _storageClient.ListObjectsAsync(bucketName, prefix, listOptions).WithCancellation(ct))
        {
            names.Add(obj.Name);
        }
        return names;
    }

    public async Task SaveFileAsync(
        string bucketName,
        string objectPath,
        string content,
        string contentType = "text/plain",
        CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await SaveFileBytesAsync(bucketName, objectPath, bytes, contentType, ct);
    }

    public async Task SaveFileBytesAsync(
        string bucketName,
        string objectPath,
        byte[] content,
        string contentType,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Uploading {Bytes} bytes to gs://{Bucket}/{Object} [{ContentType}]",
            content.Length, bucketName, objectPath, contentType);

        using var ms = new MemoryStream(content);
        var uploaded = await _storageClient.UploadObjectAsync(
            bucketName,
            objectPath,
            contentType,
            ms,
            cancellationToken: ct);

        _logger.LogInformation(
            "Upload complete: gs://{Bucket}/{Object} — Generation={Generation}, Size={Size}, MD5={Md5}",
            bucketName, objectPath, uploaded.Generation, uploaded.Size, uploaded.Md5Hash);

        // Post-upload verification: fetch the object metadata to confirm it is
        // accessible in the bucket and log the confirmed generation (version).
        var verified = await _storageClient.GetObjectAsync(bucketName, objectPath, cancellationToken: ct);

        _logger.LogInformation(
            "Upload verified in bucket: gs://{Bucket}/{Object} — ConfirmedGeneration={Generation}, ConfirmedSize={Size}, Versioned={IsVersioned}",
            bucketName, objectPath, verified.Generation, verified.Size,
            verified.Generation is not null ? "yes" : "unknown");
    }
}
