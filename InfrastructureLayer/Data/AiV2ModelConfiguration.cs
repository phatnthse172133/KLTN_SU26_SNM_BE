using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfrastructureLayer.Data;

internal static class AiV2ModelConfiguration
{
    public static void ConfigureAiV2(this ModelBuilder modelBuilder)
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
        ConfigureRecommendationPersistence(modelBuilder);
        ConfigureMealPlans(modelBuilder);
        ConfigureFoodAiProfile(modelBuilder);
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

    private static void ConfigureRecommendationPersistence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiRecommendationSession>(entity =>
        {
            entity.ToTable("AiRecommendationSession");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CustomerId, value.CreatedAt }, "idx_airecommendationsession_customer_created");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.OriginalQuery).HasMaxLength(4000);
            entity.Property(value => value.ParsedPreferenceJson).HasColumnType("jsonb");
            entity.Property(value => value.Latitude).HasPrecision(10, 7);
            entity.Property(value => value.Longitude).HasPrecision(10, 7);
            EnumString(entity.Property(value => value.Status), 20);
            entity.Property(value => value.ProviderName).HasMaxLength(100);
            entity.Property(value => value.ProviderModelName).HasMaxLength(100);
            entity.Property(value => value.ProviderRequestId).HasMaxLength(200);
            entity.Property(value => value.ProviderFailureCategory).HasMaxLength(50);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Customer).WithMany(value => value.AiRecommendationSessions).HasForeignKey(value => value.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<AiRecommendationFeedback>(entity =>
        {
            entity.ToTable("AiRecommendationFeedback");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => value.SessionId, "idx_airecommendationfeedback_session");
            entity.HasIndex(value => new { value.SessionId, value.FoodItemId }, "ux_airecommendationfeedback_state")
                .IsUnique().HasFilter("\"FoodItemId\" IS NOT NULL AND \"Action\" IN ('LIKED', 'DISLIKED')");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            EnumString(entity.Property(value => value.Action), 30);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Session).WithMany(value => value.Feedback).HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.FoodItem).WithMany().HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<AiRecommendationResult>(entity =>
        {
            entity.ToTable("AiRecommendationResult", table => table.HasCheckConstraint("ck_airecommendationresult_score", "\"Score\" BETWEEN 0 AND 100"));
            entity.HasKey(value => new { value.SessionId, value.FoodItemId });
            entity.HasIndex(value => new { value.SessionId, value.Rank }, "ux_airecommendationresult_session_rank").IsUnique();
            entity.Property(value => value.Score).HasPrecision(5, 2);
            EnumString(entity.Property(value => value.MatchTier), 20);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Session).WithMany(value => value.Results).HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.FoodItem).WithMany().HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMealPlans(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiMealPlanSession>(entity =>
        {
            entity.ToTable("AiMealPlanSession", table =>
            {
                table.HasCheckConstraint("ck_aimealplansession_party", "\"PartySize\" > 0");
                table.HasCheckConstraint("ck_aimealplansession_budget", "\"Budget\" > 0");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CustomerId, value.CreatedAt }, "idx_aimealplansession_customer_created");
            entity.HasIndex(value => new { value.CustomerId, value.IdempotencyKey }, "ux_aimealplansession_customer_idempotency")
                .IsUnique().HasFilter("\"CustomerId\" IS NOT NULL AND \"IdempotencyKey\" IS NOT NULL");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.Budget).HasPrecision(12, 2);
            entity.Property(value => value.DiningStyle).HasMaxLength(100);
            entity.Property(value => value.OriginalRequest).HasMaxLength(4000);
            entity.Property(value => value.ParsedPreferenceJson).HasColumnType("jsonb");
            entity.Property(value => value.IdempotencyKey).HasMaxLength(100);
            entity.Property(value => value.RequestHash).HasMaxLength(64);
            entity.Property(value => value.WarningsJson).HasColumnType("jsonb");
            entity.Property(value => value.Latitude).HasPrecision(10, 7);
            entity.Property(value => value.Longitude).HasPrecision(10, 7);
            EnumString(entity.Property(value => value.Status), 20);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Customer).WithMany(value => value.AiMealPlanSessions).HasForeignKey(value => value.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiMealPlan>(entity =>
        {
            entity.ToTable("AiMealPlan", table =>
            {
                table.HasCheckConstraint("ck_aimealplan_prices", "\"TotalPrice\" >= 0");
                table.HasCheckConstraint("ck_aimealplan_score", "\"CompatibilityScore\" BETWEEN 0 AND 100");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.SessionId, value.PlanCode }, "ux_aimealplan_session_code").IsUnique();
            entity.HasIndex(value => value.MarketId, "idx_aimealplan_market");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.PlanCode).HasMaxLength(50);
            entity.Property(value => value.PlanTitle).HasMaxLength(200);
            entity.Property(value => value.Strategy).HasMaxLength(100);
            entity.Property(value => value.Summary).HasMaxLength(1000);
            entity.Property(value => value.WarningsJson).HasColumnType("jsonb");
            entity.Property(value => value.TotalPrice).HasPrecision(12, 2);
            entity.Property(value => value.RemainingBudget).HasPrecision(12, 2);
            entity.Property(value => value.CompatibilityScore).HasPrecision(5, 2);
            entity.Property(value => value.Version).IsConcurrencyToken();
            EnumString(entity.Property(value => value.Status), 20);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Session).WithMany(value => value.Plans).HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Market).WithMany(value => value.AiMealPlans).HasForeignKey(value => value.MarketId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiMealPlanItem>(entity =>
        {
            entity.ToTable("AiMealPlanItem", table =>
            {
                table.HasCheckConstraint("ck_aimealplanitem_quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("ck_aimealplanitem_prices", "\"UnitPriceSnapshot\" >= 0 AND \"TotalPriceSnapshot\" >= 0");
                table.HasCheckConstraint("ck_aimealplanitem_score", "\"CompatibilityScore\" BETWEEN 0 AND 100");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => value.PlanId, "idx_aimealplanitem_plan");
            entity.HasIndex(value => new { value.PlanId, value.FoodItemId }, "ux_aimealplanitem_active_food").IsUnique().HasFilter("\"IsRemoved\" = false AND \"FoodItemId\" IS NOT NULL");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.FoodNameSnapshot).HasMaxLength(200);
            entity.Property(value => value.BoothNameSnapshot).HasMaxLength(200);
            entity.Property(value => value.ImageUrlSnapshot).HasMaxLength(2000);
            EnumString(entity.Property(value => value.Course), 30);
            entity.Property(value => value.UnitPriceSnapshot).HasPrecision(12, 2);
            entity.Property(value => value.TotalPriceSnapshot).HasPrecision(12, 2);
            entity.Property(value => value.CompatibilityScore).HasPrecision(5, 2);
            entity.Property(value => value.RatingSnapshot).HasPrecision(3, 2);
            entity.Property(value => value.Reason).HasMaxLength(1000);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.Plan).WithMany(value => value.Items).HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.FoodItem).WithMany().HasForeignKey(value => value.FoodItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(value => value.Booth).WithMany(value => value.AiMealPlanItems).HasForeignKey(value => value.BoothId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiMealPlanCartOperation>(entity =>
        {
            entity.ToTable("AiMealPlanCartOperation");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CustomerId, value.IdempotencyKey }, "ux_aimealplancart_customer_key").IsUnique();
            entity.HasIndex(value => value.PlanId, "idx_aimealplancart_plan");
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.IdempotencyKey).HasMaxLength(100);
            entity.Property(value => value.RequestHash).HasMaxLength(64);
            entity.Property(value => value.ResponseJson).HasColumnType("jsonb");
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne<AiMealPlan>().WithMany().HasForeignKey(value => value.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(value => value.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureFoodAiProfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FoodAiProfile>(entity =>
        {
            entity.ToTable("FoodAiProfile");
            entity.HasKey(value => value.FoodItemId);
            entity.HasIndex(value => value.ContentHash, "idx_foodaiprofile_contenthash");
            entity.Property(value => value.SearchText).HasColumnType("text");
            entity.Property(value => value.Embedding).HasColumnType("real[]");
            entity.Property(value => value.EmbeddingModel).HasMaxLength(100);
            entity.Property(value => value.ContentHash).HasMaxLength(64);
            EnumString(entity.Property(value => value.Status), 20);
            entity.Property(value => value.LastError).HasMaxLength(2000);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasOne(value => value.FoodItem).WithOne(value => value.AiProfile).HasForeignKey<FoodAiProfile>(value => value.FoodItemId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void EnumString<TEnum>(PropertyBuilder<TEnum> property, int maxLength) where TEnum : struct, Enum
        => property.HasConversion<string>().HasMaxLength(maxLength);

    private static void EnumString<TEnum>(PropertyBuilder<TEnum?> property, int maxLength) where TEnum : struct, Enum
        => property.HasConversion<string>().HasMaxLength(maxLength);
}
