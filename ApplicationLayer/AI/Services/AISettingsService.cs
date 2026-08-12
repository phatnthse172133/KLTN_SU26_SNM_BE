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
        if (!provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            provider = "Local";
        var enableExternal = false;
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
        if (!request.Provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("V1 AI provider is local-only. Configure OpenAI under the OpenAI section for V2.");
        if (request.EnableExternalProvider)
            throw AppException.BadRequest("V1 external AI provider is permanently disabled. Use OpenAI V2 configuration instead.");
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            throw AppException.BadRequest("API keys must be configured through OpenAI__ApiKey, not stored in the database.");

        await SaveSettingAsync("AIProvider.Provider", "Local", cancellationToken);
        await SaveSettingAsync("AIProvider.EnableExternalProvider", "false", cancellationToken);
        await SaveSettingAsync("AIProvider.Model", request.Model ?? string.Empty, cancellationToken);
        await SaveSettingAsync("AIProvider.BaseUrl", request.BaseUrl ?? string.Empty, cancellationToken);
        var legacyApiKey = await _settingsRepo.FirstOrDefaultAsync(setting => setting.Key == "AIProvider.ApiKey");
        if (legacyApiKey is not null)
            _settingsRepo.Delete(legacyApiKey);

        await _settingsRepo.SaveChangesAsync();

        var hasApiKey = !string.IsNullOrWhiteSpace(_settings.CurrentValue.ApiKey);

        var response = new AISettingsResponse
        {
            Provider = "Local",
            EnableExternalProvider = false,
            Model = request.Model ?? string.Empty,
            BaseUrl = request.BaseUrl ?? string.Empty,
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
