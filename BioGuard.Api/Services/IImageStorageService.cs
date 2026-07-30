namespace BioGuard.Api.Services;

public interface IImageStorageService
{
    Task<ImageUploadResult> UploadAsync(string base64Image, string? fileName = null);
    Task<bool> DeleteAsync(string imageUrl);
}

public record ImageUploadResult(bool Success, string? Url, string? Error);
