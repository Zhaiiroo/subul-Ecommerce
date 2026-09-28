namespace backend.Common.Storage;

public class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";
    public const string LocalProvider = "Local";
    public const string R2Provider = "R2";

    /// <summary>
    /// Headroom added on top of the file size for multipart boundaries, headers
    /// and the other form fields that travel with an upload. Shared by the
    /// Kestrel/form limits in Program.cs and by ImageUploadSizeLimitAttribute so
    /// the two cannot drift.
    /// </summary>
    public const long MultipartOverheadBytes = 65_536;

    public string Provider { get; set; } = LocalProvider;

    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    public string[] AllowedExtensions { get; set; } =
        [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public R2ImageStorageOptions R2 { get; set; } = new();

    public bool UsesLocalStorage => Provider.Equals(LocalProvider, StringComparison.OrdinalIgnoreCase);

    public bool UsesR2Storage => Provider.Equals(R2Provider, StringComparison.OrdinalIgnoreCase);
}

public class R2ImageStorageOptions
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
}
