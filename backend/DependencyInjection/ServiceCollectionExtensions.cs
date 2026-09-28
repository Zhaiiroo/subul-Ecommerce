using backend.Common.Auth;
using backend.Common.Behaviors;
using backend.Common.RateLimiting;
using backend.Common.Storage;
using backend.Infrastructure.Persistence;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace backend.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<Program>());

        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        var redisConnectionString = configuration.GetConnectionString("Redis");

        services.AddOptions<RedisRateLimitOptions>()
            .Bind(configuration.GetSection(RedisRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0, "RedisRateLimit:PermitLimit must be greater than zero.")
            .Validate(options => options.WindowSeconds > 0, "RedisRateLimit:WindowSeconds must be greater than zero.")
            .Validate(options => options.LoginPermitLimit > 0, "RedisRateLimit:LoginPermitLimit must be greater than zero.")
            .Validate(options => options.LoginWindowSeconds > 0, "RedisRateLimit:LoginWindowSeconds must be greater than zero.")
            // Enabled without a Redis connection used to boot fine and silently
            // enforce nothing: the limiter is only registered below when the
            // connection string is present, and the middleware passes every
            // request through when it cannot resolve one. Refuse to start
            // instead, so a missing environment variable is loud rather than
            // invisible. Turning the feature off stays an explicit choice.
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(redisConnectionString),
                "RedisRateLimit:Enabled is true but ConnectionStrings:Redis is not configured. " +
                "Set the connection string, or set RedisRateLimit:Enabled to false to run without rate limiting.")
            .ValidateOnStart();

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<RedisRateLimiter>();

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "subul:";
            });
        }

        services.AddOutputCache(options =>
        {
            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                options.AddBasePolicy(policy => policy
                    .With(context => context.HttpContext.Request.Path.StartsWithSegments("/api/categories"))
                    .Expire(TimeSpan.FromSeconds(30))
                    .Tag("categories")
                    .Cache());
            }
        });

        var imageStorageSection = configuration.GetSection(ImageStorageOptions.SectionName);
        var imageStorageOptions = imageStorageSection.Get<ImageStorageOptions>() ?? new ImageStorageOptions();

        services.AddOptions<ImageStorageOptions>()
            .Bind(imageStorageSection)
            .Validate(
                options => options.UsesLocalStorage || options.UsesR2Storage,
                "ImageStorage:Provider must be either 'Local' or 'R2'.")
            .Validate(
                options => options.MaxFileSizeBytes > 0,
                "ImageStorage:MaxFileSizeBytes must be greater than zero.")
            .Validate(
                options => options.AllowedExtensions.Length > 0,
                "ImageStorage:AllowedExtensions must contain at least one extension.")
            .Validate(
                options => !options.UsesR2Storage || HasValidR2Configuration(options.R2),
                "R2 image storage requires valid ServiceUrl, BucketName, AccessKeyId, SecretAccessKey, and PublicBaseUrl values.")
            .ValidateOnStart();

        if (imageStorageOptions.UsesR2Storage)
        {
            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
                new BasicAWSCredentials(
                    imageStorageOptions.R2.AccessKeyId,
                    imageStorageOptions.R2.SecretAccessKey),
                new AmazonS3Config
                {
                    ServiceURL = imageStorageOptions.R2.ServiceUrl,
                    AuthenticationRegion = "auto",
                    ForcePathStyle = true,
                }));
            services.AddSingleton<IImageStorageService, R2ImageStorageService>();
        }
        else
        {
            services.AddSingleton<IImageStorageService, LocalImageStorageService>();
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        return services;
    }

    private static bool HasValidR2Configuration(R2ImageStorageOptions options) =>
        Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var serviceUri) &&
        serviceUri.Scheme == Uri.UriSchemeHttps &&
        Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicUri) &&
        publicUri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(options.BucketName) &&
        !string.IsNullOrWhiteSpace(options.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(options.SecretAccessKey);
}
