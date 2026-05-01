using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using TruckDelivery.Driver.Application.Interfaces;

namespace TruckDelivery.Driver.Infrastructure.Services;

public sealed class MinIOStorageService(IMinioClient minioClient, IConfiguration configuration) : IStorageService
{
    private const string DriverDocsBucket = "driver-documents";
    private const string BreakdownPhotosBucket = "breakdown-photos";
    private static readonly TimeSpan UrlTtl = TimeSpan.FromMinutes(15);

    public Task<IReadOnlyList<PresignedUrlEntry>> GenerateDriverDocumentUrlsAsync(
        Guid driverId, IEnumerable<string> fields, CancellationToken ct = default)
        => GeneratePresignedUrlsAsync(DriverDocsBucket, driverId, fields, ct);

    public Task<IReadOnlyList<PresignedUrlEntry>> GenerateBreakdownPhotoUrlsAsync(
        Guid driverId, int count, CancellationToken ct = default)
    {
        var fields = Enumerable.Range(1, count).Select(i => $"photo-{i}");
        return GeneratePresignedUrlsAsync(BreakdownPhotosBucket, driverId, fields, ct);
    }

    private async Task<IReadOnlyList<PresignedUrlEntry>> GeneratePresignedUrlsAsync(
        string bucket, Guid ownerId, IEnumerable<string> fields, CancellationToken ct)
    {
        var publicEndpoint = configuration["MinIO:PublicEndpoint"]
            ?? configuration["MinIO:Endpoint"]
            ?? "http://localhost:9000";

        var results = new List<PresignedUrlEntry>();
        foreach (var field in fields)
        {
            var objectName = $"{ownerId}/{field}-{Guid.NewGuid():N}.jpg";

            var uploadUrl = await minioClient.PresignedPutObjectAsync(
                new PresignedPutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName)
                    .WithExpiry((int)UrlTtl.TotalSeconds), ct);

            var finalUrl = $"{publicEndpoint}/{bucket}/{objectName}";
            results.Add(new PresignedUrlEntry(field, uploadUrl, finalUrl));
        }

        return results;
    }
}
