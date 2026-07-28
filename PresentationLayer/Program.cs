using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Storage;
using ApplicationLayer.Configuration;
using InfrastructureLayer;
using InfrastructureLayer.Backgrounds;
using InfrastructureLayer.Cores.JWTs;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using InfrastructureLayer.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using PayOS;
using PresentationLayer.Hubs;
using PresentationLayer.Middlewares;
using PresentationLayer.Filters;
using InfrastructureLayer.Health;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using static DomainLayer.Enums.GeneralEnum;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Load .env file (environment variables override appsettings.json)
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envPath))
    envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    SNMContextFactory.LoadDotEnv(envPath);

// Re-add environment variables so .env values take effect
builder.Configuration.AddEnvironmentVariables();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
    throw new InvalidOperationException("JWT secret is missing. Set Jwt__SecretKey in PresentationLayer/.env or Jwt:SecretKey in appsettings.json (minimum 32 characters).");

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
    throw new InvalidOperationException(
        "Database connection is missing. Set ConnectionStrings__DefaultConnection.");

// Đăng ký PayIn Client với Key là "PayIn"
builder.Services
    .AddOptions<PayOSSettings>()
    .Bind(builder.Configuration.GetSection(PayOSSettings.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ClientId), "PayOS:ClientId is required.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ApiKey), "PayOS:ApiKey is required.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ChecksumKey), "PayOS:ChecksumKey is required.")
    .Validate(settings => Uri.TryCreate(settings.ReturnUrl, UriKind.Absolute, out _), "PayOS:ReturnUrl must be an absolute URL.")
    .Validate(settings => Uri.TryCreate(settings.CancelUrl, UriKind.Absolute, out _), "PayOS:CancelUrl must be an absolute URL.");

builder.Services.AddKeyedSingleton<PayOSClient>("PayIn", (sp, key) =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayOSSettings>>().Value;
    return new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
});

// Đăng ký PayOut Client với Key là "PayOut"
builder.Services.AddKeyedSingleton<PayOSClient>("PayOut", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = config.GetSection("PayOS:PayOut");
    return new PayOSClient(
        settings["ClientId"] ?? string.Empty,
        settings["ApiKey"] ?? string.Empty,
        settings["ChecksumKey"] ?? string.Empty);
});

//await payOSClient.Webhooks.ConfirmAsync("https://your-url.com/payos-webhook");

// Đăng ký Background Service dọn dẹp đơn hàng treo
//builder.Services.AddHostedService<OrderCleanupBackgroundService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = ApiResponse<ErrorResponse>.Failure(
            "Too many requests. Please try again later.",
            "RATE_LIMITED",
            new ErrorResponse
            {
                TraceId = context.HttpContext.TraceIdentifier,
                ErrorCode = "RATE_LIMITED",
                Details = "The request rate limit was exceeded."
            });
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };
    options.AddPolicy("OrderApiPolicy", httpContext =>
    {
        // Lấy UserId từ Token (nếu đã login)
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? httpContext.User.FindFirst("sub")?.Value
                     ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,                     // Chỉ cho phép 1 request
                Window = TimeSpan.FromSeconds(5),    // Trong cửa sổ 5 giây
                QueueLimit = 0                       // Không xếp hàng, gọi thừa là REJECT ngay
            });
    });
    options.AddPolicy("AuthAbusePolicy", httpContext =>
    {
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpointKey = httpContext.Request.Path.Value?.ToLowerInvariant() ?? "auth";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{endpointKey}:{clientKey}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    options.AddPolicy("AuthSessionPolicy", httpContext =>
    {
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpointKey = httpContext.Request.Path.Value?.ToLowerInvariant() ?? "auth-session";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{endpointKey}:{clientKey}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    options.AddPolicy("AIApiPolicy", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var endpoint = httpContext.Request.Path.Value?.ToLowerInvariant() ?? "ai";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{endpoint}:{userId}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    options.AddPolicy("ChatSendPolicy", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"chat-send:{userId}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
    options.AddPolicy("AvatarUploadPolicy", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"avatar-upload:{userId}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});




const string CustomerAppCorsPolicy = "CustomerAppCorsPolicy";

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Cast<string>()
    .Concat((builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CustomerAppCorsPolicy, policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (configuredCorsOrigins.Length > 0)
            policy.WithOrigins(configuredCorsOrigins);

        if (builder.Configuration.GetValue<bool>("Cors:AllowCredentials"))
            policy.AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render terminates TLS at a dynamically addressed reverse proxy. The service
    // itself is isolated behind that proxy, so there is no stable proxy IP to list.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<EnumValidationFilter>();
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<EnumValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = string.Join(
                " ",
                context.ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message)));
            var response = ApiResponse<ErrorResponse>.Failure(
                "Request validation failed.",
                "VALIDATION_ERROR",
                new ErrorResponse
                {
                    TraceId = context.HttpContext.TraceIdentifier,
                    ErrorCode = "VALIDATION_ERROR",
                    Details = details
                });
            return new BadRequestObjectResult(response);
        };
    });
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Email,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && (context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications")
                        || context.HttpContext.Request.Path.StartsWithSegments("/hubs/chats")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(subject, out var userId))
                {
                    context.Fail("Invalid user claim.");
                    return;
                }
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<SNMDbContext>();
                var isActive = await dbContext.Users.AnyAsync(user => user.Id == userId && user.Status == UserStatus.Active);

                if (!isActive) context.Fail("Account is not active.");
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<ErrorResponse>.Failure(
                        "Authentication is required.",
                        "UNAUTHORIZED",
                        new ErrorResponse
                        {
                            TraceId = context.HttpContext.TraceIdentifier,
                            ErrorCode = "UNAUTHORIZED"
                        }));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<ErrorResponse>.Failure(
                        "You do not have permission to access this resource.",
                        "FORBIDDEN",
                        new ErrorResponse
                        {
                            TraceId = context.HttpContext.TraceIdentifier,
                            ErrorCode = "FORBIDDEN"
                        }));
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddScoped<ApplicationLayer.Services.Chats.IRealtimeChatPublisher, SignalRChatPublisher>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("postgresql", tags: ["ready"]);
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

var app = builder.Build();
var swaggerEnabled = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled");

// Demo data is opt-in. A normal application start must never mutate a shared database.
if (IntegrationDemoDataSeeder.IsEnabled(builder.Configuration))
{
    await IntegrationDemoDataSeeder.SeedAsync(app.Services);
}

// Package seeders are explicit opt-ins so a normal startup never mutates shared data.
if (app.Configuration.GetValue<bool>("SeedData:PackageImages"))
{
    await InfrastructureLayer.Data.Seeders.PackageImageSeeder.SeedAsync(app.Services);
}

if (app.Configuration.GetValue<bool>("SeedData:PackagePolicies"))
{
    await InfrastructureLayer.Data.Seeders.PackagePolicySeeder.SeedAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.Services
        .GetRequiredService<AutoMapper.IMapper>()
        .ConfigurationProvider
        .AssertConfigurationIsValid();
}

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (builder.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();

app.UseCors(CustomerAppCorsPolicy);

app.UseStaticFiles();

var uploadRoot = builder.Configuration["UploadStorage:RootPath"];
if (!string.IsNullOrWhiteSpace(uploadRoot))
{
    var absoluteUploadRoot = Path.GetFullPath(uploadRoot);
    Directory.CreateDirectory(absoluteUploadRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(absoluteUploadRoot),
        RequestPath = "/uploads"
    });
}

app.UseAuthentication();

app.UseAuthorization();

// Thêm Middleware
app.UseRateLimiter();

// Liveness intentionally checks only whether the process can serve HTTP.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
// Readiness validates dependencies required by data-backed endpoints.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chats");



app.Run();
