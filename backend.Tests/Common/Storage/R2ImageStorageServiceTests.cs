using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using backend.Common.Storage;
using backend.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests.Common.Storage;

public class R2ImageStorageServiceTests
{
    [Fact]
    public async Task SaveProductImage_ValidPng_UploadsToOwnedPrefixAndReturnsPublicUrl()
    {
        using var s3 = new RecordingAmazonS3Client();
        var service = CreateService(s3);

        var result = await service.SaveProductImageAsync(
            42,
            ProductImageTestHelpers.CreatePngFormFile(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("https://media.example.com/products/42/", result.Value);
        Assert.EndsWith(".png", result.Value);
        Assert.NotNull(s3.Upload);
        Assert.StartsWith("products/42/", s3.Upload.Key);
        Assert.EndsWith(".png", s3.Upload.Key);
        Assert.Equal("subul-media", s3.Upload.BucketName);
        Assert.Equal("image/png", s3.Upload.ContentType);
        Assert.Equal("public, max-age=31536000, immutable", s3.Upload.CacheControl);
    }

    [Fact]
    public async Task SaveProductImage_InvalidImage_DoesNotCallR2()
    {
        using var s3 = new RecordingAmazonS3Client();
        var service = CreateService(s3);

        var result = await service.SaveProductImageAsync(
            42,
            ProductImageTestHelpers.CreateFakePngFormFile(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(s3.Upload);
    }

    [Fact]
    public async Task Delete_OwnedPublicUrl_DeletesMatchingObject()
    {
        using var s3 = new RecordingAmazonS3Client();
        var service = CreateService(s3);

        await service.DeleteByRelativePathAsync(
            "https://media.example.com/products/42/image.png",
            CancellationToken.None);

        Assert.NotNull(s3.Delete);
        Assert.Equal("subul-media", s3.Delete.BucketName);
        Assert.Equal("products/42/image.png", s3.Delete.Key);
    }

    [Fact]
    public async Task Delete_UrlOutsideConfiguredPublicOrigin_DoesNotCallR2()
    {
        using var s3 = new RecordingAmazonS3Client();
        var service = CreateService(s3);

        await service.DeleteByRelativePathAsync(
            "https://attacker.example/products/42/image.png",
            CancellationToken.None);

        Assert.Null(s3.Delete);
    }

    private static R2ImageStorageService CreateService(IAmazonS3 s3)
    {
        var options = Options.Create(new ImageStorageOptions
        {
            Provider = ImageStorageOptions.R2Provider,
            R2 = new R2ImageStorageOptions
            {
                ServiceUrl = "https://account-id.r2.cloudflarestorage.com",
                BucketName = "subul-media",
                AccessKeyId = "test-access-key",
                SecretAccessKey = "test-secret-key",
                PublicBaseUrl = "https://media.example.com",
            },
        });

        return new R2ImageStorageService(
            s3,
            options,
            NullLogger<R2ImageStorageService>.Instance);
    }

    private sealed class RecordingAmazonS3Client() : AmazonS3Client(
        new AnonymousAWSCredentials(),
        new AmazonS3Config
        {
            ServiceURL = "http://127.0.0.1",
            ForcePathStyle = true,
        })
    {
        public RecordedUpload? Upload { get; private set; }

        public DeleteObjectRequest? Delete { get; private set; }

        public override Task<PutObjectResponse> PutObjectAsync(
            PutObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            Upload = new RecordedUpload(
                request.BucketName,
                request.Key,
                request.ContentType,
                request.Headers.CacheControl);

            return Task.FromResult(new PutObjectResponse());
        }

        public override Task<DeleteObjectResponse> DeleteObjectAsync(
            DeleteObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            Delete = request;
            return Task.FromResult(new DeleteObjectResponse());
        }
    }

    private sealed record RecordedUpload(
        string BucketName,
        string Key,
        string ContentType,
        string CacheControl);
}
