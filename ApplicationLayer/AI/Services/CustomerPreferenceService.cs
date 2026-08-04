using ApplicationLayer.AI.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.AI.Services;

public class CustomerPreferenceService : ICustomerPreferenceService
{
    private readonly ICustomerPreferenceRepository _preferences;
    private readonly IFoodTagRepository _foodTags;
    private readonly ILegacyCustomerPreferenceAdapter? _legacyAdapter;

    public CustomerPreferenceService(
        ICustomerPreferenceRepository preferences,
        IFoodTagRepository foodTags)
    {
        _preferences = preferences;
        _foodTags = foodTags;
    }

    public CustomerPreferenceService(ICustomerPreferenceRepository preferences, IFoodTagRepository foodTags, ILegacyCustomerPreferenceAdapter legacyAdapter)
        : this(preferences, foodTags) => _legacyAdapter = legacyAdapter;

    public async Task<ApiResponse<CustomerPreferenceResponse>> GetMineAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (_legacyAdapter is not null)
        {
            var derived = await _legacyAdapter.GetDerivedAsync(customerId, cancellationToken);
            if (derived is not null) return ApiResponse<CustomerPreferenceResponse>.SuccessResponse(derived, "Deprecated response derived from normalized customer food profile.");
        }
        var preferences = await _preferences.GetByCustomerAsync(customerId, cancellationToken);
        return ApiResponse<CustomerPreferenceResponse>.SuccessResponse(Map(preferences));
    }

    public async Task<ApiResponse<CustomerPreferenceResponse>> UpdateMineAsync(
        Guid customerId,
        UpdateCustomerPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var likedIds = request.LikedTagIds.Distinct().ToList();
        var avoidIds = request.AvoidTagIds.Distinct().ToList();
        if (likedIds.Intersect(avoidIds).Any())
            throw AppException.BadRequest("A tag cannot be both liked and avoided.");

        var allIds = likedIds.Concat(avoidIds).Distinct().ToList();
        var tags = await _foodTags.GetActiveByIdsAsync(allIds, cancellationToken);
        if (tags.Count != allIds.Count)
            throw AppException.BadRequest("One or more food tags are invalid.");
        if (tags.Any(tag => !tag.IsPreferenceSelectable))
            throw AppException.BadRequest("One or more food tags cannot be selected as a customer preference.");

        var existing = await _preferences.GetByCustomerAsync(customerId, cancellationToken);
        if (existing.Count > 0)
        {
            _preferences.DeleteRange(existing);
        }

        var now = DateTime.UtcNow;
        var newPreferences = likedIds.Select(tagId => CreatePreference(customerId, tagId, CustomerPreferenceKind.Like, now))
            .Concat(avoidIds.Select(tagId => CreatePreference(customerId, tagId, CustomerPreferenceKind.Avoid, now)))
            .ToList();

        if (newPreferences.Count > 0)
        {
            await _preferences.AddRangeAsync(newPreferences);
        }

        await _preferences.SaveChangesAsync();
        var saved = await _preferences.GetByCustomerAsync(customerId, cancellationToken);
        if (_legacyAdapter is not null) await _legacyAdapter.AddSupportedAsync(customerId, saved, cancellationToken);

        return ApiResponse<CustomerPreferenceResponse>.SuccessResponse(
            Map(saved),
            "Customer preferences updated successfully.");
    }

    private static CustomerPreference CreatePreference(
        Guid customerId,
        Guid tagId,
        CustomerPreferenceKind kind,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            FoodTagId = tagId,
            PreferenceKind = kind,
            PreferenceSource = CustomerPreferenceSource.UserSelected,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static CustomerPreferenceResponse Map(IReadOnlyCollection<CustomerPreference> preferences)
        => new()
        {
            LikedTags = preferences
                .Where(preference => preference.PreferenceKind == CustomerPreferenceKind.Like)
                .Select(MapTag)
                .ToList(),
            AvoidTags = preferences
                .Where(preference => preference.PreferenceKind == CustomerPreferenceKind.Avoid)
                .Select(MapTag)
                .ToList()
        };

    private static CustomerPreferenceTagResponse MapTag(CustomerPreference preference)
        => new()
        {
            FoodTagId = preference.FoodTagId,
            Code = preference.FoodTag.Code,
            DisplayName = string.IsNullOrWhiteSpace(preference.FoodTag.Description)
                ? preference.FoodTag.Name
                : preference.FoodTag.Description
        };
}
