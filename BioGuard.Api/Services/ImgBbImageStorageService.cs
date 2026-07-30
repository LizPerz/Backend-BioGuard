using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BioGuard.Api.Config;

namespace BioGuard.Api.Services;

public class ImgBbImageStorageService : IImageStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImgBbImageStorageService> _logger;
    private readonly ImgBbOptions _options;

    public ImgBbImageStorageService(HttpClient httpClient, IOptions<ImgBbOptions> options, ILogger<ImgBbImageStorageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImageUploadResult> UploadAsync(string base64Image, string? fileName = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogWarning("ImgBB API key not configured");
                return new ImageUploadResult(false, null, "ImgBB no configurado");
            }

            var base64Data = base64Image;
            if (base64Image.Contains(','))
                base64Data = base64Image.Split(',')[1];

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("key", _options.ApiKey),
                new KeyValuePair<string, string>("image", base64Data),
                new KeyValuePair<string, string>("name", fileName ?? $"bioguard_{Guid.NewGuid():N}")
            });

            var response = await _httpClient.PostAsync(_options.BaseUrl, content);
            var json = await response.Content.ReadFromJsonAsync<ImgBbResponse>();

            if (json?.Success == true && json.Data?.Url != null)
            {
                _logger.LogInformation("Image uploaded to ImgBB: {Url}", json.Data.Url);
                return new ImageUploadResult(true, json.Data.Url, null);
            }

            _logger.LogWarning("ImgBB upload failed: {Error}", json?.Error?.Message ?? "unknown");
            return new ImageUploadResult(false, null, json?.Error?.Message ?? "Error al subir imagen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to ImgBB");
            return new ImageUploadResult(false, null, ex.Message);
        }
    }

    public async Task<bool> DeleteAsync(string imageUrl)
    {
        await Task.CompletedTask;
        _logger.LogInformation("ImgBB delete requested (not supported in free tier): {Url}", imageUrl);
        return false;
    }

    private class ImgBbResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public ImgBbData? Data { get; set; }

        [JsonPropertyName("error")]
        public ImgBbError? Error { get; set; }
    }

    private class ImgBbData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("display_url")]
        public string? DisplayUrl { get; set; }
    }

    private class ImgBbError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
