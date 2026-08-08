using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Menus;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Moq;
using static DomainLayer.Enums.GeneralEnum;
using System.Text.Json;

namespace TestingLayer;

public sealed class AiV2CutoverTests
{
    [Fact]
    public void V2Contracts_UseStrictStringEnumCodes()
    {
        var request = JsonSerializer.Deserialize<CreateFoodItemV2Request>("""{"categoryId":"00000000-0000-0000-0000-000000000001","name":"Food","price":1,"primaryCourse":"MAIN_COURSE","additionalCourses":["DRINK"],"spiceLevel":"MILD","estimatedServingCount":1}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(FoodCourse.MAIN_COURSE, request!.PrimaryCourse);
        Assert.Equal(FoodCourse.DRINK, request.AdditionalCourses.Single());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateFoodItemV2Request>("""{"primaryCourse":"NOT_A_COURSE"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void FoodAiProfile_IsDeterministic_IgnoresTransientAndLegacyData_AndMarksPendingOnSourceChange()
    {
        var food = Food();
        food.FoodItemTags.Add(new() { FoodTagId = Guid.NewGuid(), FoodTag = new() { Code = "OTHER_QUICK_SERVE", Name = "Quick" } });
        var generator = new FoodAiProfileGenerator();
        var now = DateTime.UtcNow;
        generator.Rebuild(food, now);
        var hash = food.AiProfile!.ContentHash;
        Assert.Equal(FoodAiProfileStatus.PENDING, food.AiProfile.Status);
        Assert.DoesNotContain("quick", food.AiProfile.SearchText, StringComparison.OrdinalIgnoreCase);
        food.Price += 10000; food.IsAvailable = false;
        generator.Rebuild(food, now.AddMinutes(1));
        Assert.Equal(hash, food.AiProfile.ContentHash);
        // Taste is not part of source hash — READY/PENDING stay without source invalidation.
        food.TasteProfiles.Add(new() { FoodItemId = food.Id, TasteProfileId = Guid.NewGuid(), TasteProfile = Catalog<TasteProfile>("TASTE_SWEET") });
        generator.Rebuild(food, now.AddMinutes(2));
        Assert.Equal(hash, food.AiProfile.ContentHash);
        Assert.Equal(FoodAiProfileStatus.PENDING, food.AiProfile.Status);
        Assert.Contains("taste_sweet", food.AiProfile.SearchText, StringComparison.OrdinalIgnoreCase);
        food.Name = "Bò nướng mật ong";
        food.AiProfile.Status = FoodAiProfileStatus.READY;
        food.AiProfile.AiDescription = "old";
        food.AiProfile.Embedding = [0.1f];
        generator.Rebuild(food, now.AddMinutes(3));
        Assert.NotEqual(hash, food.AiProfile.ContentHash);
        Assert.Equal(FoodAiProfileStatus.PENDING, food.AiProfile.Status);
        Assert.Null(food.AiProfile.AiDescription);
        Assert.Null(food.AiProfile.Embedding);
    }

    [Fact]
    public void FoodAiProfile_ReadyUnchangedSource_SkipsRegeneration()
    {
        var food = Food();
        var generator = new FoodAiProfileGenerator();
        var now = DateTime.UtcNow;
        generator.Rebuild(food, now);
        food.AiProfile!.Status = FoodAiProfileStatus.READY;
        food.AiProfile.AiDescription = "kept";
        food.AiProfile.SearchText = "custom-ai-merged-text";
        var hash = food.AiProfile.ContentHash;
        food.TasteProfiles.Add(new() { FoodItemId = food.Id, TasteProfileId = Guid.NewGuid(), TasteProfile = Catalog<TasteProfile>("TASTE_SWEET") });
        generator.Rebuild(food, now.AddMinutes(1));
        Assert.Equal(hash, food.AiProfile.ContentHash);
        Assert.Equal(FoodAiProfileStatus.READY, food.AiProfile.Status);
        Assert.Equal("kept", food.AiProfile.AiDescription);
        Assert.Equal("custom-ai-merged-text", food.AiProfile.SearchText);
    }

    [Fact]
    public async Task FoodAiProfileRebuild_SupportsSingleAndBoundedBatch()
    {
        var first = Food(); var second = Food();
        var foods = new Mock<IFoodItemRepository>();
        foods.Setup(x => x.GetSemanticProfileBatchAsync(first.Id, 1, It.IsAny<CancellationToken>())).ReturnsAsync([first]);
        foods.Setup(x => x.GetSemanticProfileBatchAsync(null, 2, It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        foods.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var service = new FoodAiProfileRebuildService(foods.Object, new FoodAiProfileGenerator());
        Assert.True(await service.RebuildOneAsync(first.Id));
        Assert.Equal(2, await service.RebuildBatchAsync(2));
        Assert.NotNull(first.AiProfile); Assert.NotNull(second.AiProfile);
        foods.Verify(x => x.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task MetadataCatalog_ReturnsOnlyRepositoryApprovedActiveValues()
    {
        var ingredient = Catalog<Ingredient>("ING_BEEF"); ingredient.NormalizedName = "BEEF";
        var repository = new Mock<IFoodSemanticMetadataRepository>();
        repository.Setup(x => x.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FoodSemanticCatalogSet([ingredient], [], [], [], []));
        var response = await new FoodMetadataCatalogService(repository.Object).GetActiveAsync();
        Assert.Equal(ingredient.Id, response.Data!.Ingredients.Single().Id);
    }

    [Fact]
    public async Task MenuV2_Create_WritesNormalizedMetadata_AndOwnerDeclarationIsNotAdminConfirmed()
    {
        var owner = Guid.NewGuid(); var boothId = Guid.NewGuid(); var category = new FoodCategory { Id = Guid.NewGuid(), BoothId = boothId, Name = "Main", Code = "MAIN" };
        var ingredient = Catalog<Ingredient>("ING_BEEF"); ingredient.NormalizedName = "BEEF";
        var allergen = Catalog<Allergen>("ALLERGEN_PEANUT");
        FoodItem? saved = null;
        var foodRepo = new Mock<IFoodItemRepository>();
        foodRepo.Setup(x => x.AddAsync(It.IsAny<FoodItem>())).Callback<FoodItem>(x => saved = x).Returns(Task.CompletedTask);
        foodRepo.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var service = MenuService(owner, boothId, category, foodRepo, metadata =>
        {
            metadata.Setup(x => x.GetActiveIngredientsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([ingredient]);
            metadata.Setup(x => x.GetActiveAllergensAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([allergen]);
        });
        var result = await service.CreateFoodItemV2Async(owner, boothId, new CreateFoodItemV2Request
        {
            CategoryId = category.Id, Name = "Bò nướng", Price = 90000, PrimaryCourse = FoodCourse.MAIN_COURSE,
            EstimatedServingCount = 2, IngredientIds = [ingredient.Id],
            ConfirmedAllergenDeclarations = [new() { AllergenId = allergen.Id, DeclarationType = AllergenDeclarationType.MAY_CONTAIN }]
        });
        Assert.NotNull(saved);
        Assert.True(saved!.Courses.Single().IsPrimary);
        Assert.Equal(2, saved.EstimatedServingCount);
        Assert.Equal(MetadataSource.OWNER_DECLARED, saved.Allergens.Single().Source);
        Assert.False(saved.Allergens.Single().IsConfirmed);
        Assert.NotNull(saved.AiProfile);
        Assert.Equal(FoodAiProfileStatus.PENDING, saved.AiProfile!.Status);
        Assert.Equal(FoodCourse.MAIN_COURSE, result.Data!.SemanticMetadata.PrimaryCourse);
    }

    [Fact]
    public async Task MenuV2_RejectsInactiveOrUnknownCatalogId()
    {
        var owner = Guid.NewGuid(); var boothId = Guid.NewGuid(); var category = new FoodCategory { Id = Guid.NewGuid(), BoothId = boothId, Name = "Main", Code = "MAIN" };
        var service = MenuService(owner, boothId, category, new Mock<IFoodItemRepository>(), _ => { });
        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateFoodItemV2Async(owner, boothId, new CreateFoodItemV2Request
        {
            CategoryId = category.Id, Name = "Food", Price = 1, PrimaryCourse = FoodCourse.MAIN_COURSE,
            EstimatedServingCount = 1, IngredientIds = [Guid.NewGuid()]
        }));
        Assert.Contains("invalid or inactive", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MenuV2_Update_ReplacesOwnerSets_ButPreservesAdminVerifiedDeclarations()
    {
        var owner = Guid.NewGuid(); var boothId = Guid.NewGuid(); var category = new FoodCategory { Id = Guid.NewGuid(), BoothId = boothId, Name = "Main", Code = "MAIN" };
        var oldIngredient = Catalog<Ingredient>("ING_OLD"); oldIngredient.NormalizedName = "OLD";
        var newIngredient = Catalog<Ingredient>("ING_NEW"); newIngredient.NormalizedName = "NEW";
        var adminAllergen = Catalog<Allergen>("ALLERGEN_ADMIN");
        var food = Food(); food.BoothId = boothId; food.CategoryId = category.Id; food.Category = category;
        food.Ingredients.Add(new() { FoodItemId = food.Id, IngredientId = oldIngredient.Id, Ingredient = oldIngredient });
        food.Allergens.Add(new() { FoodItemId = food.Id, AllergenId = adminAllergen.Id, Allergen = adminAllergen, Source = MetadataSource.ADMIN_VERIFIED, IsConfirmed = true });
        var foods = new Mock<IFoodItemRepository>(); foods.Setup(x => x.GetByBoothAsync(boothId, food.Id)).ReturnsAsync(food); foods.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        var service = MenuService(owner, boothId, category, foods, metadata =>
            metadata.Setup(x => x.GetActiveIngredientsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([newIngredient]));
        await service.UpdateFoodItemV2Async(owner, boothId, food.Id, new UpdateFoodItemV2Request
        {
            CategoryId = category.Id, Name = food.Name, Price = food.Price, PrimaryCourse = FoodCourse.DESSERT,
            EstimatedServingCount = 1, IngredientIds = [newIngredient.Id]
        });
        Assert.Equal(newIngredient.Id, food.Ingredients.Single().IngredientId);
        Assert.Equal(FoodCourse.DESSERT, food.Courses.Single().Course);
        Assert.True(food.Allergens.Single().IsConfirmed);
        Assert.Equal(MetadataSource.ADMIN_VERIFIED, food.Allergens.Single().Source);
    }

    [Fact]
    public async Task CustomerProfile_RejectsHardSoftConflict()
    {
        var id = Guid.NewGuid();
        var service = new CustomerFoodProfileService(new Mock<IFoodSemanticMetadataRepository>().Object);
        var error = await Assert.ThrowsAsync<AppException>(() => service.UpdateMineAsync(Guid.NewGuid(), new UpdateCustomerFoodProfileRequest
        {
            PreferredIngredientIds = [id], AvoidedIngredientIds = [id]
        }));
        Assert.Contains("cannot also be preferred", error.Message);
    }

    [Fact]
    public async Task CustomerProfile_ReplacesSetsWithoutDuplicates_AndUsesCallerIdentity()
    {
        var customer = Guid.NewGuid(); var ingredient = Catalog<Ingredient>("ING_BEEF"); ingredient.NormalizedName = "BEEF";
        var profile = new CustomerFoodProfile { CustomerId = customer, CreatedAt = DateTime.UtcNow };
        var repository = new Mock<IFoodSemanticMetadataRepository>();
        repository.Setup(x => x.GetCustomerProfileAsync(customer, true, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        repository.Setup(x => x.GetActiveIngredientsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([ingredient]);
        repository.Setup(x => x.GetActiveAllergensAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.GetActiveDietaryAttributesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.GetActivePreparationMethodsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.GetActiveTasteProfilesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var response = await new CustomerFoodProfileService(repository.Object).UpdateMineAsync(customer, new() { AvoidedIngredientIds = [ingredient.Id], PreferredCourses = [FoodCourse.DRINK] });
        Assert.Equal(customer, profile.AvoidedIngredients.Single().CustomerId);
        Assert.Equal(FoodCourse.DRINK, profile.PreferredCourses.Single().Course);
        Assert.Single(response.Data!.AvoidedIngredients);
        repository.Verify(x => x.GetCustomerProfileAsync(customer, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MenuService MenuService(Guid owner, Guid boothId, FoodCategory category, Mock<IFoodItemRepository> foodRepo, Action<Mock<IFoodSemanticMetadataRepository>> configure)
    {
        var booths = new Mock<IBoothRepository>(); booths.Setup(x => x.GetOwnedBoothAsync(owner, boothId)).ReturnsAsync(new Booth { Id = boothId, BoothOwnerId = owner, Status = BoothStatus.Active });
        var categories = new Mock<IFoodCategoryRepository>(); categories.Setup(x => x.GetActiveByBoothAsync(boothId, category.Id)).ReturnsAsync(category);
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(x => x.GetActiveIngredientsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        metadata.Setup(x => x.GetActiveAllergensAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        metadata.Setup(x => x.GetActiveDietaryAttributesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        metadata.Setup(x => x.GetActivePreparationMethodsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        metadata.Setup(x => x.GetActiveTasteProfilesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        configure(metadata);
        return new MenuService(booths.Object, categories.Object, foodRepo.Object, new Mock<IFoodTagRepository>().Object, new Mock<IMapper>().Object,
            metadata.Object, new FoodAiProfileGenerator(), new Mock<ILegacyFoodTagMetadataAdapter>().Object);
    }

    private static FoodItem Food() => new()
    {
        Id = Guid.NewGuid(), Name = "Bò nướng", Description = "Cay nhẹ", Price = 100,
        IsAvailable = true, Category = new FoodCategory { Id = Guid.NewGuid(), Code = "MAIN", Name = "Main" },
        Courses = [new() { Course = FoodCourse.MAIN_COURSE, IsPrimary = true }]
    };
    private static T Catalog<T>(string code) where T : SemanticCatalogEntity, new()
        => new() { Id = Guid.NewGuid(), Code = code, Name = code, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}
