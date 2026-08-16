using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfrastructureLayer.Data;

internal static class FoodMetadataModelConfiguration
{
    public static void ConfigureFoodMetadataModel(this ModelBuilder modelBuilder)
    {
        ConfigureCatalog(modelBuilder.Entity<Ingredient>(), "Ingredient");
        modelBuilder.Entity<Ingredient>().Property(value => value.NormalizedName).HasMaxLength(200);
        modelBuilder.Entity<Ingredient>().HasIndex(value => value.NormalizedName, "idx_ingredient_normalized_name");
        ConfigureCatalog(modelBuilder.Entity<Allergen>(), "Allergen");
        ConfigureCatalog(modelBuilder.Entity<DietaryAttribute>(), "DietaryAttribute");
        ConfigureCatalog(modelBuilder.Entity<PreparationMethod>(), "PreparationMethod");
        ConfigureCatalog(modelBuilder.Entity<TasteProfile>(), "TasteProfile");
        ConfigureCatalog(modelBuilder.Entity<FoodSearchFacet>(), "FoodSearchFacet");

        ConfigureFoodMetadata(modelBuilder);
        ConfigureCustomerProfiles(modelBuilder);
    }

    private static void ConfigureCatalog<T>(EntityTypeBuilder<T> entity, string tableName)
        where T : SemanticCatalogEntity
    {
        entity.ToTable(tableName, table => table.HasCheckConstraint($"ck_{tableName.ToLowerInvariant()}_code_normalized", $"\"Code\" = upper(btrim(\"Code\"))"));
        entity.HasKey(value => value.Id).HasName($"{tableName}_pkey");
        entity.HasIndex(value => value.Code, $"ux_{tableName.ToLowerInvariant()}_code").IsUnique();
        entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(value => value.Code).HasMaxLength(100);
        entity.Property(value => value.Name).HasMaxLength(200);
        entity.Property(value => value.IsActive).HasDefaultValue(true);
        entity.Property(value => value.IsSystem).HasDefaultValue(false);
        entity.Property(value => value.DisplayOrder).HasDefaultValue(0);
        entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
        entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
    }

    private static void ConfigureFoodMetadata(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FoodItemIngredient>(entity =>
        {
            entity.ToTable("FoodItemIngredient");
            entity.HasKey(value => new { value.FoodItemId, value.IngredientId });
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.Ingredients).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Ingredient).WithMany(value => value.FoodItems).HasForeignKey(value => value.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemAllergen>(entity =>
        {
            entity.ToTable("FoodItemAllergen");
            entity.HasKey(value => new { value.FoodItemId, value.AllergenId });
            EnumString(entity.Property(value => value.DeclarationType), 20);
            EnumString(entity.Property(value => value.Source), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.Allergens).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Allergen).WithMany(value => value.FoodItems).HasForeignKey(value => value.AllergenId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemDietaryAttribute>(entity =>
        {
            entity.ToTable("FoodItemDietaryAttribute");
            entity.HasKey(value => new { value.FoodItemId, value.DietaryAttributeId });
            EnumString(entity.Property(value => value.SuitabilityStatus), 20);
            EnumString(entity.Property(value => value.Source), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.DietaryAttributes).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.DietaryAttribute).WithMany(value => value.FoodItems).HasForeignKey(value => value.DietaryAttributeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemPreparationMethod>(entity =>
        {
            entity.ToTable("FoodItemPreparationMethod");
            entity.HasKey(value => new { value.FoodItemId, value.PreparationMethodId });
            entity.HasIndex(value => value.FoodItemId, "ux_fooditempreparation_primary").IsUnique().HasFilter("\"IsPrimary\" = true");
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.PreparationMethods).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.PreparationMethod).WithMany(value => value.FoodItems).HasForeignKey(value => value.PreparationMethodId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemTasteProfile>(entity =>
        {
            entity.ToTable("FoodItemTasteProfile", table => table.HasCheckConstraint("ck_fooditemtaste_intensity", "\"Intensity\" IS NULL OR (\"Intensity\" BETWEEN 1 AND 5)"));
            entity.HasKey(value => new { value.FoodItemId, value.TasteProfileId });
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.TasteProfiles).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.TasteProfile).WithMany(value => value.FoodItems).HasForeignKey(value => value.TasteProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemSearchFacet>(entity =>
        {
            entity.ToTable("FoodItemSearchFacet");
            entity.HasKey(value => new { value.FoodItemId, value.FoodSearchFacetId });
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.SearchFacets).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.FoodSearchFacet).WithMany(value => value.FoodItems).HasForeignKey(value => value.FoodSearchFacetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodItemCourse>(entity =>
        {
            entity.ToTable("FoodItemCourse");
            entity.HasKey(value => new { value.FoodItemId, value.Course });
            entity.HasIndex(value => value.FoodItemId, "ux_fooditemcourse_primary").IsUnique().HasFilter("\"IsPrimary\" = true");
            EnumString(entity.Property(value => value.Course), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.Courses).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FoodItemDiningPurpose>(entity =>
        {
            entity.ToTable("FoodItemDiningPurpose");
            entity.HasKey(value => new { value.FoodItemId, value.Purpose });
            EnumString(entity.Property(value => value.Purpose), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithMany(value => value.DiningPurposes).HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCustomerProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerFoodProfile>(entity =>
        {
            entity.ToTable("CustomerFoodProfile", table =>
            {
                table.HasCheckConstraint("ck_customerfoodprofile_price_range", "\"PreferredPriceMin\" IS NULL OR \"PreferredPriceMax\" IS NULL OR \"PreferredPriceMin\" <= \"PreferredPriceMax\"");
                table.HasCheckConstraint("ck_customerfoodprofile_distance", "\"DefaultMaxDistanceMeters\" IS NULL OR \"DefaultMaxDistanceMeters\" > 0");
            });
            entity.HasKey(value => value.CustomerId);
            EnumString(entity.Property(value => value.PreferredSpiceLevel), 20);
            entity.Property(value => value.PreferredPriceMin).HasPrecision(12, 2);
            entity.Property(value => value.PreferredPriceMax).HasPrecision(12, 2);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Customer).WithOne(value => value.CustomerFoodProfile).HasForeignKey<CustomerFoodProfile>(value => value.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureCustomerCatalogJoin<CustomerPreferredIngredient, Ingredient>(modelBuilder, "CustomerPreferredIngredient", "IngredientId", value => value.PreferredIngredients);
        ConfigureCustomerCatalogJoin<CustomerAvoidedIngredient, Ingredient>(modelBuilder, "CustomerAvoidedIngredient", "IngredientId", value => value.AvoidedIngredients);
        ConfigureCustomerCatalogJoin<CustomerDietaryRequirement, DietaryAttribute>(modelBuilder, "CustomerDietaryRequirement", "DietaryAttributeId", value => value.DietaryRequirements);
        ConfigureCustomerCatalogJoin<CustomerAllergenExclusion, Allergen>(modelBuilder, "CustomerAllergenExclusion", "AllergenId", value => value.AllergenExclusions);
        ConfigureCustomerCatalogJoin<CustomerPreferredPreparationMethod, PreparationMethod>(modelBuilder, "CustomerPreferredPreparationMethod", "PreparationMethodId", value => value.PreferredPreparationMethods);
        ConfigureCustomerCatalogJoin<CustomerPreferredTasteProfile, TasteProfile>(modelBuilder, "CustomerPreferredTasteProfile", "TasteProfileId", value => value.PreferredTasteProfiles);
        ConfigureCustomerCatalogJoin<CustomerAvoidedTasteProfile, TasteProfile>(modelBuilder, "CustomerAvoidedTasteProfile", "TasteProfileId", value => value.AvoidedTasteProfiles);

        modelBuilder.Entity<CustomerPreferredCourse>(entity =>
        {
            entity.ToTable("CustomerPreferredCourse");
            entity.HasKey(value => new { value.CustomerId, value.Course });
            EnumString(entity.Property(value => value.Course), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.CustomerFoodProfile).WithMany(value => value.PreferredCourses).HasForeignKey(value => value.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CustomerPreferredDiningPurpose>(entity =>
        {
            entity.ToTable("CustomerPreferredDiningPurpose");
            entity.HasKey(value => new { value.CustomerId, value.Purpose });
            EnumString(entity.Property(value => value.Purpose), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.CustomerFoodProfile).WithMany(value => value.PreferredDiningPurposes).HasForeignKey(value => value.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCustomerCatalogJoin<TJoin, TCatalog>(
        ModelBuilder modelBuilder,
        string tableName,
        string catalogKey,
        System.Linq.Expressions.Expression<Func<CustomerFoodProfile, IEnumerable<TJoin>?>> navigation)
        where TJoin : class
        where TCatalog : SemanticCatalogEntity
    {
        var entity = modelBuilder.Entity<TJoin>();
        entity.ToTable(tableName);
        entity.HasKey("CustomerId", catalogKey);
        entity.Property<DateTime>("CreatedAt").HasDefaultValueSql("now()");
        entity.HasOne<CustomerFoodProfile>("CustomerFoodProfile").WithMany(navigation).HasForeignKey("CustomerId").OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<TCatalog>(typeof(TCatalog).Name).WithMany().HasForeignKey(catalogKey).OnDelete(DeleteBehavior.Restrict);
    }

    private static void EnumString<TEnum>(PropertyBuilder<TEnum> property, int maxLength) where TEnum : struct, Enum
        => property.HasConversion<string>().HasMaxLength(maxLength);

    private static void EnumString<TEnum>(PropertyBuilder<TEnum?> property, int maxLength) where TEnum : struct, Enum
        => property.HasConversion<string>().HasMaxLength(maxLength);
}
