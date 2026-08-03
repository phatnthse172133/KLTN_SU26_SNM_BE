using DomainLayer.Entities;
using DomainLayer.InterfaceCore.JWT;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data.Seeders;

public static class AISeedData
{
    public static bool IsEnabled(IConfiguration configuration)
        => string.Equals(configuration["SeedDemoData"], "true", StringComparison.OrdinalIgnoreCase);

    private static readonly IReadOnlyCollection<FoodTagSeed> TagSeeds =
    [
        new("11111111-1111-1111-1111-111111111001", "Spicy", "SPICY", FoodTagGroup.Taste, "Cay"),
        new("11111111-1111-1111-1111-111111111003", "Mild", "MILD", FoodTagGroup.Taste, "Vị nhẹ"),
        new("11111111-1111-1111-1111-111111111004", "Grilled", "GRILLED", FoodTagGroup.CookingMethod, "Đồ nướng"),
        new("11111111-1111-1111-1111-111111111005", "Fried", "FRIED", FoodTagGroup.CookingMethod, "Đồ chiên"),
        new("11111111-1111-1111-1111-111111111006", "Soup", "SOUP", FoodTagGroup.CookingMethod, "Món nước"),
        new("11111111-1111-1111-1111-111111111007", "Hot", "HOT", FoodTagGroup.Temperature, "Món nóng"),
        new("11111111-1111-1111-1111-111111111009", "FullMeal", "FULLMEAL", FoodTagGroup.MealPurpose, "Ăn no"),
        new("11111111-1111-1111-1111-111111111010", "Snack", "SNACK", FoodTagGroup.MealPurpose, "Ăn vặt"),
        new("11111111-1111-1111-1111-111111111011", "Drink", "DRINK", FoodTagGroup.MealPurpose, "Đồ uống"),
        new("11111111-1111-1111-1111-111111111012", "Dessert", "DESSERT", FoodTagGroup.MealPurpose, "Tráng miệng"),
        new("11111111-1111-1111-1111-111111111013", "Shareable", "SHAREABLE", FoodTagGroup.MealPurpose, "Phù hợp ăn nhóm"),
        new("11111111-1111-1111-1111-111111111014", "Chicken", "CHICKEN", FoodTagGroup.Ingredient, "Gà"),
        new("11111111-1111-1111-1111-111111111015", "Beef", "BEEF", FoodTagGroup.Ingredient, "Bò"),
        new("11111111-1111-1111-1111-111111111016", "Pork", "PORK", FoodTagGroup.Ingredient, "Heo"),
        new("11111111-1111-1111-1111-111111111017", "Seafood", "SEAFOOD", FoodTagGroup.Ingredient, "Hải sản"),
        new("11111111-1111-1111-1111-111111111019", "Noodle", "NOODLE", FoodTagGroup.Ingredient, "Mì / bún / phở"),
        new("11111111-1111-1111-1111-111111111020", "Rice", "RICE", FoodTagGroup.Ingredient, "Cơm"),
        new("11111111-1111-1111-1111-111111111023", "BudgetFriendly", "BUDGETFRIENDLY", FoodTagGroup.Budget, "Giá tốt"),
        new("11111111-1111-1111-1111-111111111024", "MidRange", "MIDRANGE", FoodTagGroup.Budget, "Giá trung bình"),
        new("11111111-1111-1111-1111-111111111026", "Vietnamese", "VIETNAMESE", FoodTagGroup.Other, "Món Việt")
    ];

    private static readonly Guid CustomerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid BoothOwnerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222002");
    private static readonly Guid DemoCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222101");
    private static readonly Guid AlternateDemoCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222102");
    private const string LegacyDemoPasswordHash = "SNM_DEMO_HASH_REPLACE_BEFORE_REAL_LOGIN";

    private static readonly IReadOnlyCollection<MarketSeed> MarketSeeds =
    [
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333001"),
            "Chợ đêm Bến Thành",
            "Khu chợ trung tâm Quận 1; ban ngày là chợ truyền thống, buổi tối nổi bật với hàng ăn và mua sắm quanh các trục Phan Bội Châu, Phan Chu Trinh.",
            "Đường Lê Lợi, phường Bến Thành, Quận 1, TP. Hồ Chí Minh",
            10.7721m,
            106.6983m,
            new TimeOnly(18, 0),
            new TimeOnly(22, 0),
            420,
            260),
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333002"),
            "Phố ẩm thực Hồ Thị Kỷ",
            "Khu ẩm thực đêm trong lòng chợ hoa Hồ Thị Kỷ, nổi tiếng với món Việt, món Campuchia, đồ nướng, chè và đồ uống đường phố.",
            "Hồ Thị Kỷ, Phường 1, Quận 10, TP. Hồ Chí Minh",
            10.7637m,
            106.6707m,
            new TimeOnly(0, 0),
            new TimeOnly(23, 59),
            520,
            220),
    ];

    private static readonly IReadOnlyCollection<ZoneSeed> ZoneSeeds =
    [
        new(Guid.Parse("44444444-4444-4444-4444-444444444001"), Guid.Parse("33333333-3333-3333-3333-333333333001"), "Cổng Nam - Lê Lợi", "Khu vào chính gần quảng trường Quách Thị Trang, phù hợp điểm hẹn và món ăn nhanh.", "#2F6B4F"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444002"), Guid.Parse("33333333-3333-3333-3333-333333333001"), "Cánh Đông - Phan Bội Châu", "Khu hàng ăn tối và các quầy ngồi lại dọc Phan Bội Châu.", "#D9713C"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444003"), Guid.Parse("33333333-3333-3333-3333-333333333001"), "Cánh Tây - Phan Chu Trinh", "Khu hàng lưu niệm, đồ khô và các quầy ăn tối bên hông chợ.", "#2B6CB0"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444004"), Guid.Parse("33333333-3333-3333-3333-333333333002"), "Lối chợ hoa", "Khu đầu tuyến gần các sạp hoa, nhiều đồ uống và món ăn nhẹ.", "#8B5CF6"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444005"), Guid.Parse("33333333-3333-3333-3333-333333333002"), "Hẻm ẩm thực Campuchia", "Cụm món Campuchia và món nướng đặc trưng của phố Hồ Thị Kỷ.", "#B56A00"),
        new(Guid.Parse("44444444-4444-4444-4444-444444444006"), Guid.Parse("33333333-3333-3333-3333-333333333002"), "Khu chè và đồ uống", "Khu tráng miệng, chè, trà tắc, nước mát và món lạnh.", "#2F7D52")
    ];

    private static readonly IReadOnlyCollection<BoothSeed> BoothSeeds =
    [
        new(1, Guid.Parse("33333333-3333-3333-3333-333333333001"), Guid.Parse("44444444-4444-4444-4444-444444444002"), "BT-PBC-01", "Quầy demo hải sản nướng Bến Thành", "Hải sản nướng, sò, tôm và mực theo phong cách chợ đêm Bến Thành.", 4.7m, 92, 72),
        new(2, Guid.Parse("33333333-3333-3333-3333-333333333001"), Guid.Parse("44444444-4444-4444-4444-444444444001"), "BT-LL-02", "Quầy demo cơm tấm - bún thịt nướng", "Các món Việt ăn no, phù hợp khách du lịch và nhóm nhỏ.", 4.5m, 44, 58),
        new(3, Guid.Parse("33333333-3333-3333-3333-333333333001"), Guid.Parse("44444444-4444-4444-4444-444444444003"), "BT-PCT-03", "Quầy demo chè và nước mát", "Chè, nước sâm, trà tắc và món tráng miệng mát.", 4.4m, 150, 64),
        new(4, Guid.Parse("33333333-3333-3333-3333-333333333002"), Guid.Parse("44444444-4444-4444-4444-444444444005"), "HTK-KH-01", "Quầy demo món Campuchia Hồ Thị Kỷ", "Bún num bò chóc, bò nướng lá lốt và các món đậm vị trong khu Hồ Thị Kỷ.", 4.8m, 78, 86),
        new(5, Guid.Parse("33333333-3333-3333-3333-333333333002"), Guid.Parse("44444444-4444-4444-4444-444444444004"), "HTK-HOA-02", "Quầy demo xiên nướng Hồ Thị Kỷ", "Xiên nướng, bánh tráng và món ăn vặt nóng cho nhóm bạn.", 4.6m, 120, 50),
        new(6, Guid.Parse("33333333-3333-3333-3333-333333333002"), Guid.Parse("44444444-4444-4444-4444-444444444006"), "HTK-NUOC-03", "Quầy demo chè - trà tắc Hồ Thị Kỷ", "Chè Thái, trà tắc, nước mát và món lạnh giá tốt.", 4.5m, 172, 80)
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SNMDbContext>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var configuredPassword = configuration["SeedDemoDataPassword"];
        var demoPasswordHash = passwordHasher.HashPassword(
            string.IsNullOrWhiteSpace(configuredPassword)
                ? Guid.NewGuid().ToString("N")
                : configuredPassword);

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            logger.LogInformation(
                "SeedDemoDataPassword is not configured. Demo users will be seeded with a valid but non-loginable random password.");
        }

        try
        {
            await SeedTagsAsync(dbContext);
            await SeedDemoMarketsAsync(dbContext);
            await SeedDemoUsersAsync(
                dbContext,
                demoPasswordHash,
                configuredPassword,
                passwordHasher);
            await SeedDemoBoothsAndMenuAsync(dbContext);
            await SeedDemoCustomerPreferencesAsync(dbContext);
            await SeedFoodItemTagsAsync(dbContext);
            await dbContext.SaveChangesAsync();
            await SeedEffectivePricesAsync(dbContext);
            await SeedDemoHistoryAsync(dbContext);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AI seed data could not be applied.");
            throw;
        }
    }

    private static async Task SeedTagsAsync(SNMDbContext dbContext)
    {
        var existingCodes = await dbContext.FoodTags
            .Select(tag => tag.Code)
            .ToListAsync();
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var seed in TagSeeds.Where(seed => !existingSet.Contains(seed.Code)))
        {
            dbContext.FoodTags.Add(new FoodTag
            {
                Id = Guid.Parse(seed.Id),
                Name = seed.Name,
                Code = seed.Code,
                Description = seed.Description,
                TagGroup = seed.TagGroup,
                Status = FoodTagStatus.Active,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static async Task SeedDemoMarketsAsync(SNMDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var existingMarketIds = (await dbContext.NightMarkets.Select(market => market.Id).ToListAsync()).ToHashSet();
        foreach (var seed in MarketSeeds.Where(seed => !existingMarketIds.Contains(seed.Id)))
        {
            dbContext.NightMarkets.Add(new NightMarket
            {
                Id = seed.Id,
                Name = seed.Name,
                Description = seed.Description,
                Address = seed.Address,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                OpeningHours = seed.OpenTime,
                ClosingHours = seed.CloseTime,
                BoundaryWidthMeters = seed.WidthMeters,
                BoundaryHeightMeters = seed.HeightMeters,
                TotalBooth = BoothSeeds.Count(booth => booth.MarketId == seed.Id),
                Status = NightMarketStatus.Active,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        var existingZoneIds = (await dbContext.Zones.Select(zone => zone.Id).ToListAsync()).ToHashSet();
        foreach (var seed in ZoneSeeds.Where(seed => !existingZoneIds.Contains(seed.Id)))
        {
            dbContext.Zones.Add(new Zone
            {
                Id = seed.Id,
                NightMarketId = seed.MarketId,
                ZoneName = seed.Name,
                Description = seed.Description,
                Color = seed.Color,
                Status = ZoneStatus.Active,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Known demo records converge on every explicit seed run. The second market stays
        // orderable throughout the day so staging acceptance is independent of server time.
        foreach (var seed in MarketSeeds)
        {
            var market = await dbContext.NightMarkets.FindAsync(seed.Id);
            if (market is null)
            {
                continue;
            }

            market.OpeningHours = seed.OpenTime;
            market.ClosingHours = seed.CloseTime;
            market.TotalBooth = BoothSeeds.Count(booth => booth.MarketId == seed.Id);
            market.Status = NightMarketStatus.Active;
            market.ModerationStatus = ModerationStatus.Active;
            market.IsDeleted = false;
            market.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDemoUsersAsync(
        SNMDbContext dbContext,
        string demoPasswordHash,
        string? configuredPassword,
        IPasswordHasher passwordHasher)
    {
        var now = DateTime.UtcNow;
        var customerRole = await dbContext.Roles.FirstOrDefaultAsync(role =>
            role.Id == CustomerRoleId || role.RoleName == "Customer");
        if (customerRole is null)
        {
            customerRole = new Role
            {
                Id = CustomerRoleId,
                RoleName = "Customer",
                Description = "Khách hàng sử dụng ứng dụng",
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Roles.Add(customerRole);
        }

        var boothOwnerRole = await dbContext.Roles.FirstOrDefaultAsync(role =>
            role.Id == BoothOwnerRoleId || role.RoleName == "BoothOwner");
        if (boothOwnerRole is null)
        {
            boothOwnerRole = new Role
            {
                Id = BoothOwnerRoleId,
                RoleName = "BoothOwner",
                Description = "Chủ gian hàng",
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Roles.Add(boothOwnerRole);
        }

        await dbContext.SaveChangesAsync();
        var customerRoleId = customerRole.Id;
        var boothOwnerRoleId = boothOwnerRole.Id;

        if (!await dbContext.Users.AnyAsync(user => user.Id == DemoCustomerId))
        {
            dbContext.Users.Add(new User
            {
                Id = DemoCustomerId,
                RoleId = customerRoleId,
                UserName = "demo.customer.ai",
                PasswordHash = demoPasswordHash,
                FullName = "Khách demo AI",
                Email = "demo.customer.ai@snm.local",
                Phone = "0900000101",
                Address = "TP. Hồ Chí Minh",
                Status = UserStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        else
        {
            var demoCustomer = await dbContext.Users.SingleAsync(user => user.Id == DemoCustomerId);
            if (ShouldSynchronizeDemoPassword(demoCustomer.PasswordHash, configuredPassword, passwordHasher))
            {
                demoCustomer.PasswordHash = demoPasswordHash;
                demoCustomer.UpdatedAt = now;
            }
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == AlternateDemoCustomerId))
        {
            dbContext.Users.Add(new User
            {
                Id = AlternateDemoCustomerId,
                RoleId = customerRoleId,
                UserName = "demo.customer.ai.alternate",
                PasswordHash = demoPasswordHash,
                FullName = "Khách demo AI thay thế",
                Email = "demo.customer.ai.alternate@snm.local",
                Phone = "0900000102",
                Address = "Đà Lạt",
                Status = UserStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            var alternateCustomer = await dbContext.Users.SingleAsync(user => user.Id == AlternateDemoCustomerId);
            if (ShouldSynchronizeDemoPassword(alternateCustomer.PasswordHash, configuredPassword, passwordHasher))
            {
                alternateCustomer.PasswordHash = demoPasswordHash;
                alternateCustomer.UpdatedAt = now;
            }
        }

        foreach (var seed in BoothSeeds)
        {
            var ownerId = OwnerId(seed.Index);
            var existingOwner = await dbContext.Users.SingleOrDefaultAsync(user => user.Id == ownerId);
            if (existingOwner is not null)
            {
                if (ShouldSynchronizeDemoPassword(existingOwner.PasswordHash, configuredPassword, passwordHasher))
                {
                    existingOwner.PasswordHash = demoPasswordHash;
                    existingOwner.UpdatedAt = now;
                }

                continue;
            }

            dbContext.Users.Add(new User
            {
                Id = ownerId,
                RoleId = boothOwnerRoleId,
                UserName = $"demo.booth.owner.{seed.Index:00}",
                PasswordHash = demoPasswordHash,
                FullName = $"Chủ gian hàng demo {seed.Index:00}",
                Email = $"demo.booth.owner.{seed.Index:00}@snm.local",
                Phone = $"09000002{seed.Index:00}",
                Address = "Việt Nam",
                Status = UserStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static bool ShouldSynchronizeDemoPassword(
        string currentHash,
        string? configuredPassword,
        IPasswordHasher passwordHasher)
    {
        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return currentHash == LegacyDemoPasswordHash;
        }

        if (currentHash == LegacyDemoPasswordHash)
        {
            return true;
        }

        try
        {
            return !passwordHasher.VerifyPassword(configuredPassword, currentHash);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static async Task SeedDemoBoothsAndMenuAsync(SNMDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var existingRegistrationIds = (await dbContext.BoothRegistrations.Select(registration => registration.Id).ToListAsync()).ToHashSet();
        foreach (var seed in BoothSeeds.Where(seed => !existingRegistrationIds.Contains(RegistrationId(seed.Index))))
        {
            dbContext.BoothRegistrations.Add(new BoothRegistration
            {
                Id = RegistrationId(seed.Index),
                OwnerId = OwnerId(seed.Index),
                RequestedNightMarketId = seed.MarketId,
                PreferredZoneId = seed.ZoneId,
                BoothName = seed.Name,
                Description = seed.Description,
                Phone = $"09000003{seed.Index:00}",
                Status = BoothRegistrationStatus.Approved,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();

        var existingBoothIds = (await dbContext.Booths.Select(booth => booth.Id).ToListAsync()).ToHashSet();
        foreach (var seed in BoothSeeds.Where(seed => !existingBoothIds.Contains(BoothId(seed.Index))))
        {
            dbContext.Booths.Add(new Booth
            {
                Id = BoothId(seed.Index),
                RegistrationId = RegistrationId(seed.Index),
                NightMarketId = seed.MarketId,
                BoothOwnerId = OwnerId(seed.Index),
                ZoneId = seed.ZoneId,
                BoothName = seed.Name,
                BoothCode = seed.Code,
                Description = seed.Description,
                PhoneNumber = $"09000003{seed.Index:00}",
                SlotNumber = seed.Code,
                MapPositionX = seed.X,
                MapPositionY = seed.Y,
                OpenTime = seed.MarketId == MarketSeeds.Last().Id ? new TimeOnly(0, 0) : new TimeOnly(17, 0),
                CloseTime = seed.MarketId == MarketSeeds.Last().Id ? new TimeOnly(23, 59) : new TimeOnly(23, 0),
                AverageRating = seed.Rating,
                IsFeatured = seed.Index is 4 or 7,
                PackageName = "Demo AI",
                Status = BoothStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync();

        foreach (var seed in BoothSeeds)
        {
            var booth = await dbContext.Booths.FindAsync(BoothId(seed.Index));
            if (booth is null)
            {
                continue;
            }

            booth.OpenTime = seed.MarketId == MarketSeeds.Last().Id ? new TimeOnly(0, 0) : new TimeOnly(17, 0);
            booth.CloseTime = seed.MarketId == MarketSeeds.Last().Id ? new TimeOnly(23, 59) : new TimeOnly(23, 0);
            booth.Status = BoothStatus.Active;
            booth.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync();

        foreach (var seed in BoothSeeds)
        {
            var categoryId = CategoryId(seed.Index);
            if (!await dbContext.FoodCategories.AnyAsync(category => category.Id == categoryId))
            {
                dbContext.FoodCategories.Add(new FoodCategory
                {
                    Id = categoryId,
                    BoothId = BoothId(seed.Index),
                    Code = $"DEMO_AI_{seed.Index:00}",
                    Name = "Thực đơn demo AI",
                    Description = "Danh mục món seed để AI có dữ liệu thật khi gợi ý.",
                    IsDeleted = false,
                    IsActive = true,
                    IsSelectable = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            var foodIndex = 1;
            foreach (var food in BuildMenu(seed.Index))
            {
                var foodId = FoodId(seed.Index, foodIndex);
                if (await dbContext.FoodItems.AnyAsync(item => item.Id == foodId))
                {
                    foodIndex++;
                    continue;
                }

                dbContext.FoodItems.Add(new FoodItem
                {
                    Id = foodId,
                    BoothId = BoothId(seed.Index),
                    CategoryId = categoryId,
                    Name = food.Name,
                    Description = food.Description,
                    Price = food.Price,
                    IsAvailable = true,
                    IsFeatured = foodIndex == 1,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                foodIndex++;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDemoCustomerPreferencesAsync(SNMDbContext dbContext)
    {
        var tags = await dbContext.FoodTags.Where(tag => !tag.IsDeleted).ToDictionaryAsync(tag => tag.Code);
        var preferenceSeeds = new[]
        {
            (DemoCustomerId, "GRILLED", CustomerPreferenceKind.Like),
            (DemoCustomerId, "SPICY", CustomerPreferenceKind.Like),
            (DemoCustomerId, "VIETNAMESE", CustomerPreferenceKind.Like),
            (DemoCustomerId, "SEAFOOD", CustomerPreferenceKind.Avoid),
            (AlternateDemoCustomerId, "DESSERT", CustomerPreferenceKind.Like),
            (AlternateDemoCustomerId, "DRINK", CustomerPreferenceKind.Like),
            (AlternateDemoCustomerId, "SPICY", CustomerPreferenceKind.Avoid)
        };

        foreach (var (customerId, code, kind) in preferenceSeeds)
        {
            if (!tags.TryGetValue(code, out var tag))
            {
                continue;
            }

            var exists = await dbContext.CustomerPreferences.AnyAsync(preference =>
                preference.CustomerId == customerId
                && preference.FoodTagId == tag.Id
                && preference.PreferenceKind == kind);
            if (exists)
            {
                continue;
            }

            dbContext.CustomerPreferences.Add(new CustomerPreference
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                FoodTagId = tag.Id,
                PreferenceKind = kind,
                PreferenceSource = CustomerPreferenceSource.UserSelected,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private static async Task SeedEffectivePricesAsync(SNMDbContext dbContext)
    {
        var priceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
        if (await dbContext.FoodPrices.AnyAsync(price => price.Id == priceId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        dbContext.FoodPrices.Add(new FoodPrice
        {
            Id = priceId,
            FoodItemId = FoodId(4, 2),
            Price = 49000m,
            StartDate = now.AddDays(-1),
            EndDate = now.AddYears(5),
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static async Task SeedDemoHistoryAsync(SNMDbContext dbContext)
    {
        await SeedCustomerHistoryAsync(
            dbContext,
            customerId: DemoCustomerId,
            boothIndex: 5,
            foodId: FoodId(5, 3),
            orderId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"),
            orderCode: 904000001,
            paymentId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0001"),
            detailId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0001"),
            reviewId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeee0001"),
            ageInDays: 7);

        await SeedCustomerHistoryAsync(
            dbContext,
            customerId: AlternateDemoCustomerId,
            boothIndex: 3,
            foodId: FoodId(3, 1),
            orderId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002"),
            orderCode: 904000002,
            paymentId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0002"),
            detailId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0002"),
            reviewId: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeee0002"),
            ageInDays: 5);
    }

    private static async Task SeedCustomerHistoryAsync(
        SNMDbContext dbContext,
        Guid customerId,
        int boothIndex,
        Guid foodId,
        Guid orderId,
        long orderCode,
        Guid paymentId,
        Guid detailId,
        Guid reviewId,
        int ageInDays)
    {
        var food = await dbContext.FoodItems.FirstAsync(item => item.Id == foodId);
        var occurredAt = DateTime.UtcNow.AddDays(-ageInDays);

        if (!await dbContext.Orders.AnyAsync(order => order.Id == orderId))
        {
            dbContext.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                BoothOwnerId = OwnerId(boothIndex),
                OrderCode = orderCode,
                CheckoutRequestId = Guid.Parse($"ffffffff-ffff-ffff-ffff-ffffffff000{(customerId == DemoCustomerId ? 1 : 2)}"),
                Status = OrderStatus.Completed,
                TotalAmount = food.Price,
                DiscountAmount = 0,
                FinalAmount = food.Price,
                Note = "Controlled AI staging history",
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt
            });
        }

        if (!await dbContext.OrderDetails.AnyAsync(detail => detail.Id == detailId))
        {
            dbContext.OrderDetails.Add(new OrderDetail
            {
                Id = detailId,
                OrderId = orderId,
                FoodItemId = foodId,
                FoodNameSnapshot = food.Name,
                Quantity = 1,
                UnitPrice = food.Price,
                TotalPrice = food.Price,
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt
            });
        }

        if (!await dbContext.Payments.AnyAsync(payment => payment.Id == paymentId))
        {
            dbContext.Payments.Add(new Payment
            {
                Id = paymentId,
                OrderId = orderId,
                BoothOwnerId = OwnerId(boothIndex),
                Type = PaymentType.Cash,
                Gateway = PaymentGateway.None,
                Amount = food.Price,
                Status = PaymentStatus.Paid,
                PaidAt = occurredAt,
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt
            });
        }

        if (!await dbContext.Reviews.AnyAsync(review => review.Id == reviewId))
        {
            dbContext.Reviews.Add(new Review
            {
                Id = reviewId,
                BoothId = BoothId(boothIndex),
                CustomerId = customerId,
                OrderId = orderId,
                Rating = 5,
                Content = "Controlled AI staging review",
                IsVisible = true,
                CreatedAt = occurredAt.AddHours(1),
                UpdatedAt = occurredAt.AddHours(1)
            });
        }
    }

    private static async Task SeedFoodItemTagsAsync(SNMDbContext dbContext)
    {
        var tags = await dbContext.FoodTags
            .Where(tag => !tag.IsDeleted)
            .ToDictionaryAsync(tag => tag.Code);
        var taggedFoodIds = await dbContext.FoodItemTags
            .Select(tag => tag.FoodItemId)
            .Distinct()
            .ToListAsync();
        var taggedSet = taggedFoodIds.ToHashSet();
        var foodItems = await dbContext.FoodItems
            .Where(item => !item.IsDeleted && !taggedSet.Contains(item.Id))
            .ToListAsync();

        foreach (var foodItem in foodItems)
        {
            var codes = InferTagCodes(foodItem);
            foreach (var code in codes)
            {
                if (!tags.TryGetValue(code, out var tag))
                {
                    continue;
                }

                dbContext.FoodItemTags.Add(new FoodItemTag
                {
                    FoodItemId = foodItem.Id,
                    FoodTagId = tag.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }

    private static IReadOnlyCollection<string> InferTagCodes(FoodItem foodItem)
    {
        var text = $"{foodItem.Name} {foodItem.Description}".ToLowerInvariant();
        var codes = new HashSet<string>();

        AddIf(text, codes, "SPICY", "cay", "spicy", "sa te");
        AddIf(text, codes, "MILD", "nhẹ", "mild", "dễ uống");
        AddIf(text, codes, "GRILLED", "nuong", "nướng", "bbq");
        AddIf(text, codes, "FRIED", "chien", "chiên", "ran");
        AddIf(text, codes, "SOUP", "bun", "bún", "pho", "phở", "mi", "mì", "nuoc", "nước");
        AddIf(text, codes, "DRINK", "tra", "trà", "nuoc", "nước", "soda", "coffee", "ca phe");
        AddIf(text, codes, "DESSERT", "che", "chè", "kem", "banh ngot", "bánh ngọt");
        AddIf(text, codes, "CHICKEN", "ga", "gà");
        AddIf(text, codes, "BEEF", "bo", "bò");
        AddIf(text, codes, "PORK", "heo", "pork", "thit nuong", "thịt nướng");
        AddIf(text, codes, "SEAFOOD", "hai san", "hải sản", "tom", "tôm", "muc", "mực");
        AddIf(text, codes, "SHAREABLE", "chia sẻ", "ăn nhóm", "nhóm bạn");
        AddIf(text, codes, "RICE", "com", "cơm");
        AddIf(text, codes, "NOODLE", "bun", "bún", "pho", "phở", "mi", "mì");

        if (foodItem.Price <= 50000) codes.Add("BUDGETFRIENDLY");
        else if (foodItem.Price <= 120000) codes.Add("MIDRANGE");
        else codes.Add("PREMIUM");

        if (!codes.Contains("DRINK") && !codes.Contains("DESSERT"))
        {
            codes.Add(foodItem.Price >= 60000 ? "FULLMEAL" : "SNACK");
            codes.Add("HOT");
        }

        codes.Add("VIETNAMESE");
        return codes;
    }

    private static void AddIf(string text, HashSet<string> codes, string code, params string[] keywords)
    {
        if (keywords.Any(text.Contains))
        {
            codes.Add(code);
        }
    }

    private static IReadOnlyCollection<FoodSeed> BuildMenu(int boothIndex)
        => boothIndex switch
        {
            1 =>
            [
                new("Mực nướng sa tế", "Mực nướng nóng, vị cay nhẹ, hợp ăn nhóm tại khu Phan Bội Châu.", 85000m),
                new("Tôm nướng muối ớt", "Tôm nướng vỏ giòn, cay mặn kiểu chợ đêm.", 95000m),
                new("Nước sâm lạnh", "Đồ uống mát, cân bằng món nướng.", 18000m)
            ],
            2 =>
            [
                new("Cơm tấm sườn bì", "Món Việt ăn no, phù hợp khách cần bữa chính.", 65000m),
                new("Bún thịt nướng chả giò", "Bún thịt nướng, rau sống, chả giò giòn.", 60000m),
                new("Trà tắc", "Đồ uống lạnh giá tốt.", 15000m)
            ],
            3 =>
            [
                new("Chè ba màu", "Món tráng miệng lạnh phổ biến ở TP.HCM.", 25000m),
                new("Chè Thái sầu riêng", "Vị ngọt béo, dùng sau món nướng hoặc món cay.", 35000m),
                new("Nước sâm rong biển", "Đồ uống mát, hợp đi chợ đêm.", 18000m)
            ],
            4 =>
            [
                new("Bún num bò chóc", "Món Campuchia đặc trưng tại khu Hồ Thị Kỷ, vị đậm và hơi cay.", 65000m),
                new("Bò nướng lá lốt", "Món nướng nóng, thơm lá lốt, phù hợp ăn nhóm.", 55000m),
                new("Trà tắc xí muội", "Đồ uống lạnh phổ biến ở phố ẩm thực.", 18000m)
            ],
            5 =>
            [
                new("Xiên bò nướng", "Xiên nướng nóng, dễ chia sẻ cho nhóm bạn.", 25000m),
                new("Bánh tráng nướng trứng", "Món ăn vặt nóng kiểu street food Việt.", 30000m),
                new("Gà xiên nướng cay", "Gà xiên sa tế, hợp khẩu vị thích cay.", 22000m)
            ],
            6 =>
            [
                new("Chè Thái", "Chè lạnh nhiều topping, hợp sau món cay.", 30000m),
                new("Trà tắc mật ong", "Đồ uống lạnh, chua ngọt dễ uống.", 18000m),
                new("Sữa chua nếp cẩm", "Tráng miệng lạnh, vị ngọt nhẹ.", 28000m)
            ],
            7 =>
            [
                new("Bánh tráng nướng Đà Lạt", "Bánh tráng nướng trứng, hành, topping nóng giòn.", 30000m),
                new("Sữa đậu nành nóng", "Đồ uống nóng đặc trưng buổi tối Đà Lạt.", 15000m),
                new("Khoai lang nướng", "Món nóng, ngọt tự nhiên, hợp thời tiết lạnh.", 25000m),
                new("Bắp nướng mỡ hành", "Món nướng ăn vặt giá tốt.", 25000m)
            ],
            8 =>
            [
                new("Lẩu gà lá é phần nhỏ", "Món nóng ăn no, hợp nhóm 2-3 người ở Đà Lạt.", 120000m),
                new("Xiên que thập cẩm", "Xiên nóng dễ chia sẻ tại khu quảng trường chợ.", 45000m),
                new("Bò viên nóng", "Món nước nóng, dùng nhanh khi trời lạnh.", 35000m),
                new("Trà atiso nóng", "Đồ uống nóng đặc sản Đà Lạt.", 18000m)
            ],
            _ =>
            [
                new("Kem bơ Đà Lạt", "Món lạnh đặc sản, béo nhẹ và ngọt vừa.", 45000m),
                new("Dâu lắc muối ớt", "Trái cây Đà Lạt vị chua ngọt cay nhẹ.", 35000m),
                new("Sữa chua phô mai", "Tráng miệng lạnh, vị béo ngọt.", 30000m),
                new("Nước ép dâu", "Đồ uống lạnh từ dâu Đà Lạt.", 35000m)
            ]
        };

    private static Guid OwnerId(int index) => Guid.Parse($"55555555-5555-5555-5555-555555555{index:000}");
    private static Guid RegistrationId(int index) => Guid.Parse($"66666666-6666-6666-6666-666666666{index:000}");
    private static Guid BoothId(int index) => Guid.Parse($"77777777-7777-7777-7777-777777777{index:000}");
    private static Guid CategoryId(int index) => Guid.Parse($"88888888-8888-8888-8888-888888888{index:000}");
    private static Guid FoodId(int boothIndex, int foodIndex) => Guid.Parse($"99999999-9999-9999-9999-99999999{boothIndex:00}{foodIndex:00}");

    private sealed record FoodTagSeed(
        string Id,
        string Name,
        string Code,
        FoodTagGroup TagGroup,
        string Description);

    private sealed record MarketSeed(
        Guid Id,
        string Name,
        string Description,
        string Address,
        decimal Latitude,
        decimal Longitude,
        TimeOnly OpenTime,
        TimeOnly CloseTime,
        int WidthMeters,
        int HeightMeters);

    private sealed record ZoneSeed(Guid Id, Guid MarketId, string Name, string Description, string Color);

    private sealed record BoothSeed(
        int Index,
        Guid MarketId,
        Guid ZoneId,
        string Code,
        string Name,
        string Description,
        decimal Rating,
        decimal X,
        decimal Y);

    private sealed record FoodSeed(string Name, string Description, decimal Price);
}
