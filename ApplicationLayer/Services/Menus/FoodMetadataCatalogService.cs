using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Menus;

public sealed class FoodMetadataCatalogService(IFoodSemanticMetadataRepository repository) : IFoodMetadataCatalogService
{
    public async Task<ApiResponse<FoodMetadataCatalogResponse>> GetActiveAsync(CancellationToken ct = default)
    {
        var values = await repository.GetActiveCatalogsAsync(ct);
        return ApiResponse<FoodMetadataCatalogResponse>.SuccessResponse(new()
        {
            Ingredients = values.Ingredients.Select(Map).ToArray(), Allergens = values.Allergens.Select(Map).ToArray(),
            DietaryAttributes = values.DietaryAttributes.Select(Map).ToArray(), PreparationMethods = values.PreparationMethods.Select(Map).ToArray(),
            TasteProfiles = values.TasteProfiles.Select(Map).ToArray()
        });
    }
    private static SemanticCatalogResponse Map(SemanticCatalogEntity value) => new(value.Id, value.Code, value.Name);
}
