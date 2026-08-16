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
