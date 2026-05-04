namespace TruckDelivery.Driver.Application.Interfaces;

public sealed record PresignedUrlEntry(string Field, string UploadUrl, string FinalUrl);

public interface IStorageService
{
    // Returns one pre-signed PUT URL per field name for the driver-documents bucket
    Task<IReadOnlyList<PresignedUrlEntry>> GenerateDriverDocumentUrlsAsync(
        Guid driverId, IEnumerable<string> fields, CancellationToken ct = default);

    // Returns `count` pre-signed PUT URLs for the breakdown-photos bucket
    Task<IReadOnlyList<PresignedUrlEntry>> GenerateBreakdownPhotoUrlsAsync(
        Guid driverId, int count, CancellationToken ct = default);
}
