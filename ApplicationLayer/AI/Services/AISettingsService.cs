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
        var hasApiKey = dbSettings.TryGetValue("AIProvider.ApiKey", out var k) ? !string.IsNullOrWhiteSpace(k) : !string.IsNullOrWhiteSpace(configSettings.ApiKey);

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

        if (string.IsNullOrWhiteSpace(request.Model))
            throw AppException.BadRequest("Model is required.");

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            throw AppException.BadRequest("Base URL is required.");

        await SaveSettingAsync("AIProvider.Provider", request.Provider, cancellationToken);
        await SaveSettingAsync("AIProvider.EnableExternalProvider", request.EnableExternalProvider.ToString().ToLowerInvariant(), cancellationToken);
        await SaveSettingAsync("AIProvider.Model", request.Model, cancellationToken);
        await SaveSettingAsync("AIProvider.BaseUrl", request.BaseUrl, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            await SaveSettingAsync("AIProvider.ApiKey", request.ApiKey, cancellationToken);
        }

        await _settingsRepo.SaveChangesAsync();

        var hasApiKey = await _settingsRepo.AnyAsync(s => s.Key == "AIProvider.ApiKey" && s.Value != "");

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
