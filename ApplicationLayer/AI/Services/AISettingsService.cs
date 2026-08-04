using ApplicationLayer.AI;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.AI.Services;

public class AISettingsService : IAISettingsService
{
    private readonly IGenericRepository<SystemSetting> _settingsRepo;
    private readonly IOptionsMonitor<AIProviderSettings> _settings;

    public AISettingsService(IGenericRepository<SystemSetting> settingsRepo, IOptionsMonitor<AIProviderSettings> settings)
    {
        _settingsRepo = settingsRepo;
        _settings = settings;
    }

    public async Task<ApiResponse<AISettingsResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var dbSettings = await LoadFromDbAsync(cancellationToken);
        var configSettings = _settings.CurrentValue;

        var provider = dbSettings.TryGetValue("AIProvider.Provider", out var p) ? p : configSettings.Provider;
        var enableExternal = dbSettings.TryGetValue("AIProvider.EnableExternalProvider", out var e) && bool.TryParse(e, out var parsed) ? parsed : configSettings.EnableExternalProvider;
        var model = dbSettings.TryGetValue("AIProvider.Model", out var m) ? m : configSettings.Model;
        var baseUrl = dbSettings.TryGetValue("AIProvider.BaseUrl", out var b) ? b : configSettings.BaseUrl;
        var hasApiKey = !string.IsNullOrWhiteSpace(configSettings.ApiKey);

        var response = new AISettingsResponse
        {
            Provider = provider,
            EnableExternalProvider = enableExternal,
            Model = model,
            BaseUrl = baseUrl,
            HasApiKey = hasApiKey,
        };
        return ApiResponse<AISettingsResponse>.SuccessResponse(response);
    }

    public async Task<ApiResponse<AISettingsResponse>> UpdateAsync(UpdateAISettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
            throw AppException.BadRequest("Provider is required.");
        if (!request.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("Only the configured Gemini provider is supported.");

        if (string.IsNullOrWhiteSpace(request.Model))
            throw AppException.BadRequest("Model is required.");
        if (request.Model.Length > 100 || request.Model.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            throw AppException.BadRequest("Model contains unsupported characters.");

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            throw AppException.BadRequest("Base URL is required.");
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !baseUri.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("Gemini BaseUrl must use the official HTTPS host.");
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            throw AppException.BadRequest("API keys must be configured through AIProvider__ApiKey, not stored in the database.");

        await SaveSettingAsync("AIProvider.Provider", request.Provider, cancellationToken);
        await SaveSettingAsync("AIProvider.EnableExternalProvider", request.EnableExternalProvider.ToString().ToLowerInvariant(), cancellationToken);
        await SaveSettingAsync("AIProvider.Model", request.Model, cancellationToken);
        await SaveSettingAsync("AIProvider.BaseUrl", request.BaseUrl, cancellationToken);
        var legacyApiKey = await _settingsRepo.FirstOrDefaultAsync(setting => setting.Key == "AIProvider.ApiKey");
        if (legacyApiKey is not null)
            _settingsRepo.Delete(legacyApiKey);

        await _settingsRepo.SaveChangesAsync();

        var hasApiKey = !string.IsNullOrWhiteSpace(_settings.CurrentValue.ApiKey);

        var response = new AISettingsResponse
        {
            Provider = request.Provider,
            EnableExternalProvider = request.EnableExternalProvider,
            Model = request.Model,
            BaseUrl = request.BaseUrl,
            HasApiKey = hasApiKey,
        };

        return ApiResponse<AISettingsResponse>.SuccessResponse(response, "AI settings updated successfully.");
    }

    private async Task<Dictionary<string, string>> LoadFromDbAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepo.FindAsync(s => s.Key.StartsWith("AIProvider."));
        return settings.ToDictionary(s => s.Key, s => s.Value);
    }

    private async Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await _settingsRepo.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            _settingsRepo.Update(existing);
        }
        else
        {
            await _settingsRepo.AddAsync(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
    }
}
