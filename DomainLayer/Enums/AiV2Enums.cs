namespace DomainLayer.Enums;

public enum FoodCourse
{
    APPETIZER,
    MAIN_COURSE,
    DRINK,
    DESSERT,
    SIDE_DISH,
    SOUP,
    SHARED_DISH,
    EXTRA
}

public enum FoodSpiceLevel { UNKNOWN, NON_SPICY, MILD, SPICY, VERY_SPICY }
public enum ServingTemperature { UNKNOWN, COLD, ROOM, HOT }
public enum AllergenDeclarationType { CONTAINS, MAY_CONTAIN }
public enum MetadataSource { UNKNOWN, OWNER_DECLARED, SYSTEM_MIGRATED, ADMIN_VERIFIED }
public enum DietarySuitabilityStatus { UNKNOWN, UNVERIFIED, SUITABLE, NOT_SUITABLE }
public enum DiningPurpose
{
    FULL_MEAL,
    LIGHT_MEAL,
    SNACKING,
    FOOD_TOUR,
    QUICK_MEAL,
    LATE_NIGHT,
    DESSERT,
    REFRESHMENT,
    SHARING,
    TAKEAWAY
}

public enum AiSessionStatus { PENDING, PROCESSING, COMPLETED, FAILED, EXPIRED }
public enum AiRecommendationFeedbackAction
{
    VIEWED,
    LIKED,
    DISLIKED,
    OPENED_FOOD,
    OPENED_BOOTH,
    OPENED_MARKET,
    ADDED_TO_CART,
    SELECTED,
    DISMISSED
}
public enum RecommendationMatchTier { STRONG_MATCH, NEAR_MATCH, LOW_MATCH }
public enum AiMealPlanStatus { DRAFT, READY, COMPLETED, EXPIRED, FAILED }
public enum MealPlanDiningStyle
{
    FULL_MEAL, LIGHT_MEAL, FOOD_TOUR, FAMILY, DATE, FRIEND_GROUP, BUDGET_FRIENDLY, LOCAL_SPECIALTY
}
public enum MealPlanStrategy { NEAREST, BEST_MATCH, BUDGET_FRIENDLY, DIVERSE }
public enum FoodAiProfileStatus { PENDING, READY, STALE, FAILED, DISABLED }
