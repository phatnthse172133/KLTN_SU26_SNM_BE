using DomainLayer.Entities;

namespace ApplicationLayer.Services.Menus;

public interface IFoodAiProfileGenerator
{
    void Rebuild(FoodItem food, DateTime utcNow);
}
