using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using InfrastructureLayer;
using InfrastructureLayer.Backgrounds;
using InfrastructureLayer.Cores.JWTs;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PayOS;
using PresentationLayer.Hubs;
using PresentationLayer.Middlewares;
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
    DotNetEnv.Env.Load(envPath);

// Re-add environment variables so .env values take effect
builder.Configuration.AddEnvironmentVariables();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
    throw new InvalidOperationException("JWT secret is missing. Set Jwt__SecretKey in PresentationLayer/.env or Jwt:SecretKey in appsettings.json (minimum 32 characters).");

// Đăng ký PayIn Client với Key là "PayIn"
builder.Services.AddKeyedSingleton<PayOSClient>("PayIn", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = config.GetSection("PayOS:PayIn");
    return new PayOSClient(settings["ClientId"], settings["ApiKey"], settings["ChecksumKey"]);
});

// Đăng ký PayOut Client với Key là "PayOut"
builder.Services.AddKeyedSingleton<PayOSClient>("PayOut", (sp, key) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = config.GetSection("PayOS:PayOut");
    return new PayOSClient(settings["ClientId"], settings["ApiKey"], settings["ChecksumKey"]);
});

//await payOSClient.Webhooks.ConfirmAsync("https://your-url.com/payos-webhook");

// Đăng ký Background Service dọn dẹp đơn hàng treo
builder.Services.AddHostedService<OrderCleanupBackgroundService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
});




const string CustomerAppCorsPolicy = "CustomerAppCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CustomerAppCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:8081",
                "http://localhost:8082",
                "http://localhost:8083",
                "http://localhost:8084",
                "http://localhost:19006",
                "http://127.0.0.1:8081",
                "http://127.0.0.1:8082",
                "http://127.0.0.1:8083",
                "http://127.0.0.1:8084",
                "http://127.0.0.1:19006")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
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
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
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
                        new ErrorResponse
                        {
                            TraceId = context.HttpContext.TraceIdentifier,
                            ErrorCode = "FORBIDDEN"
                        }));
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotificationPublisher, SignalRNotificationPublisher>();
builder.Services.AddEndpointsApiExplorer();
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

await AISeedData.SeedAsync(app.Services);

if (app.Environment.IsDevelopment()) 
{
    app.Services
        .GetRequiredService<AutoMapper.IMapper>()
        .ConfigurationProvider
        .AssertConfigurationIsValid();
    app.UseSwagger(); 
    app.UseSwaggerUI(); 
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(CustomerAppCorsPolicy);

app.UseAuthentication();

app.UseAuthorization();

// Thêm Middleware
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");



app.Run();
