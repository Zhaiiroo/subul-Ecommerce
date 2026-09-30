using backend.Common.Middleware;
using backend.Common.RateLimiting;
using backend.Common.Storage;
using backend.Common.Auth;
using backend.DependencyInjection;
using backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Net;
using System.Text;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var maxImageBytes = builder.Configuration
    .GetSection(ImageStorageOptions.SectionName)
    .GetValue<long>("MaxFileSizeBytes", 5_242_880);
var imageStorageProvider = builder.Configuration
    .GetSection(ImageStorageOptions.SectionName)
    .GetValue<string>("Provider") ?? ImageStorageOptions.LocalProvider;
var multipartLimit = maxImageBytes + ImageStorageOptions.MultipartOverheadBytes;

// Kestrel's body limit is global. Sizing it for image uploads handed the same
// multi-megabyte allowance to every JSON endpoint, including the unauthenticated
// POST api/orders — enough to write megabytes of text per request into columns
// that have no length limit. The global cap is therefore sized for JSON, and the
// three upload actions raise it for themselves via [ImageUploadSizeLimit].
// A request over the cap is rejected by Kestrel with 413 before the body is read.
var maxJsonBodyBytes = builder.Configuration
    .GetValue<long>("RequestLimits:MaxJsonBodyBytes", 262_144);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = multipartLimit;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxJsonBodyBytes;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.Zero,
        };

        // A signed token alone cannot tell us that the account behind it was
        // disabled or had its password reset five minutes ago. AdminSessionValidator
        // reconciles both against the row before the request is authorized.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = AdminSessionValidator.ValidateAsync,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

// Behind Traefik the peer address is the proxy, not the caller. Without this the
// rate limiter buckets the entire internet under one identifier (a self-inflicted
// outage at 120 req/min) and Order.IpAddress records the proxy on every order.
//
// The default options trust only loopback, which is not the proxy's address on a
// Docker network — so the headers would be dropped silently. The trusted range is
// therefore explicit, and deliberately narrow: accepting X-Forwarded-For from
// anywhere would let a caller spoof it and mint a fresh rate-limit bucket per
// request, which is worse than not reading the header at all.
var trustedProxyNetworks = builder.Configuration
    .GetSection("ForwardedHeaders:TrustedNetworks")
    .Get<string[]>() ?? [];

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var network in trustedProxyNetworks)
    {
        var parts = network.Split('/', 2);
        if (parts.Length == 2 &&
            IPAddress.TryParse(parts[0], out var prefix) &&
            int.TryParse(parts[1], out var prefixLength))
        {
            // Qualified because Microsoft.AspNetCore.HttpOverrides declares a
            // legacy IPNetwork of its own; KnownIPNetworks takes the BCL type.
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
        }
        else if (IPAddress.TryParse(network, out var proxyAddress))
        {
            options.KnownProxies.Add(proxyAddress);
        }
        else
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:TrustedNetworks contains '{network}', which is neither an IP address nor a CIDR range.");
        }
    }
});

builder.Services.AddOptions<AdminBootstrapOptions>()
    .Bind(builder.Configuration.GetSection(AdminBootstrapOptions.SectionName))
    .Validate(
        options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Email),
        "AdminBootstrap:Email must be set while AdminBootstrap:Enabled is true.")
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Schema changes are an explicit one-shot deployment phase: the `migrate`
// service in compose.yaml runs this and exits, and `api` starts only after it
// succeeds. Normal startup never mutates the schema, so a bad migration fails a
// deploy step instead of crash-looping the API.
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    await db.Database.MigrateAsync();

    // Console rather than ILogger: Production logs at Warning, and this line is
    // the one thing an operator reads from `docker compose logs migrate`.
    Console.WriteLine(pending.Count == 0
        ? "Database schema is up to date."
        : $"Applied {pending.Count} migration(s): {string.Join(", ", pending)}");
    return;
}

// First in the pipeline: everything downstream — rate limiting, HTTPS
// redirection, logging — should see the caller's address and scheme, not the
// proxy's.
if (trustedProxyNetworks.Length > 0)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

if (corsOrigins.Length > 0)
{
    app.UseCors();
}

if (imageStorageProvider.Equals(ImageStorageOptions.LocalProvider, StringComparison.OrdinalIgnoreCase))
{
    var imgPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "img"));
    Directory.CreateDirectory(imgPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(imgPath),
        RequestPath = "/img"
    });
}

app.UseAuthentication();
app.UseMiddleware<RedisRateLimitMiddleware>();
app.UseAuthorization();
// After authorization, so an anonymous caller still gets 401 rather than 403.
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseOutputCache();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}

// Runs in every environment, but only on an empty admin_users table — in
// Development the seeder above has already filled it, so this is the production
// path that creates the single first account.
await AdminBootstrapper.RunAsync(app.Services);

app.Run();

public partial class Program { }
