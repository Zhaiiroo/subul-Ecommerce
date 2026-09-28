using Amazon.S3;
using Amazon.S3.Model;
using backend.Common.Results;
using Microsoft.Extensions.Options;

namespace backend.Common.Storage;

public class R2ImageStorageService(
    IAmazonS3 s3,
    IOptions<ImageStorageOptions> options,
    ILogger<R2ImageStorageService> logger) : IImageStorageService
{
    private readonly ImageStorageOptions _options = options.Value;
    private readonly Uri _publicBaseUri = new(
        options.Value.R2.PublicBaseUrl.TrimEnd('/') + "/",
        UriKind.Absolute);

    public Task<Result<string>> SaveProductImageAsync(
        long productId,
        IFormFile file,
        CancellationToken cancellationToken) =>
        SaveImageAsync($"products/{productId}", $"{Guid.NewGuid():N}", file, cancellationToken);

    public Task<Result<string>> SaveBrandImageAsync(
        long brandId,
        string slot,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (slot is not ("logo" or "banner"))
            return Task.FromResult(Result<string>.Failure("Invalid brand image slot"));

        return SaveImageAsync(
            $"brands/{brandId}",
            $"{slot}-{Guid.NewGuid():N}",
            file,
            cancellationToken);
    }

    private async Task<Result<string>> SaveImageAsync(
        string prefix,
        string fileBaseName,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return Result<string>.Failure("Image file is required");

        if (file.Length > _options.MaxFileSizeBytes)
            return Result<string>.Failure("Image file exceeds maximum allowed size");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) ||
            !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return Result<string>.Failure("Invalid image file");

        await using var inputStream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await inputStream.CopyToAsync(buffer, cancellationToken);

        if (!ValidateImageContent(buffer, extension))
            return Result<string>.Failure("Invalid image file");

        var key = $"{prefix}/{fileBaseName}{extension}";
        buffer.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = _options.R2.BucketName,
            Key = key,
            InputStream = buffer,
            ContentType = GetContentType(extension),
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await s3.PutObjectAsync(request, cancellationToken);

        return Result<string>.Success(new Uri(_publicBaseUri, key).AbsoluteUri);
    }

    public async Task DeleteByRelativePathAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwnedObjectKey(relativePath, out var key))
        {
            logger.LogWarning(
                "Skipping deletion for image URL outside the configured R2 public origin: {ImageUrl}",
                relativePath);
            return;
        }

        await s3.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.R2.BucketName,
                Key = key,
            },
            cancellationToken);
    }

    private bool TryGetOwnedObjectKey(string storedPath, out string key)
    {
        key = string.Empty;

        if (!Uri.TryCreate(storedPath, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(_publicBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals(_publicBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != _publicBaseUri.Port ||
            !uri.AbsolutePath.StartsWith(_publicBaseUri.AbsolutePath, StringComparison.Ordinal))
            return false;

        key = Uri.UnescapeDataString(uri.AbsolutePath[_publicBaseUri.AbsolutePath.Length..]);
        return !string.IsNullOrWhiteSpace(key) && !key.Contains("..", StringComparison.Ordinal);
    }

    private static bool ValidateImageContent(MemoryStream buffer, string extension)
    {
        buffer.Position = 0;
        var header = new byte[12];
        var bytesRead = buffer.Read(header, 0, header.Length);
        buffer.Position = 0;
        return ImageFileSignatures.MatchesExtension(header, bytesRead, extension);
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };
}
