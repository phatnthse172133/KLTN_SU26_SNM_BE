using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceCore.JWT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Data.Seeders;

// Creates the isolated Phase 03.5 integration dataset. Layout edge distances are
// physical walking distances in metres; layout X/Y values remain layout coordinates.
public static class IntegrationDemoDataSeeder
{
    public static readonly Guid MarketId = Id("100");
    public static readonly Guid LayoutId = Id("200");
    public static readonly Guid MainEntranceNodeId = Id("301");

    private static readonly Guid CustomerRoleId = Id("001");
    private static readonly Guid BoothOwnerRoleId = Id("002");
    private static readonly Guid CustomerAId = Id("010");
    private static readonly Guid CustomerBId = Id("011");
    private static readonly Guid ZoneAId = Id("201");
    private static readonly Guid ZoneBId = Id("202");

    private const string AssetRoot = "/images/demo/phase035";
    private const string MarketCoverUrl = $"{AssetRoot}/market-v2.jpg";

    private static readonly BoothSeed[] Booths =
    [
        new(1, "Bếp Than Phố Hội Demo", "Món nướng Việt Nam phục vụ theo phần nhỏ, phù hợp nhóm bạn.", "A-01", ZoneAId, 160, 110, true, new TimeOnly(0, 0), new TimeOnly(23, 59)),
        new(2, "Hải Sản Gió Biển Demo", "Hải sản nướng và hấp với mức giá minh bạch cho dữ liệu trình diễn.", "A-02", ZoneAId, 360, 110, false, new TimeOnly(16, 0), new TimeOnly(23, 59)),
        new(3, "Chè Nhà Mây Demo", "Chè và món tráng miệng Việt Nam, có lựa chọn ít ngọt.", "B-01", ZoneBId, 560, 110, true, new TimeOnly(0, 0), new TimeOnly(23, 59)),
        new(4, "Trạm Nước Mát Demo", "Trà trái cây và nước mát pha tại quầy.", "B-02", ZoneBId, 560, 310, false, new TimeOnly(8, 0), new TimeOnly(17, 0)),
        new(5, "Góc Ăn Vặt Sài Gòn Demo", "Các món ăn vặt đường phố được chuẩn hóa cho kiểm thử.", "B-03", ZoneBId, 360, 310, false, new TimeOnly(17, 0), new TimeOnly(23, 0))
    ];

    private static readonly NodeSeed[] Nodes =
    [
        new(1, "Cổng chính", LayoutNodeType.Entrance, 60, 250, null, true),
        new(2, "Giao lộ trung tâm", LayoutNodeType.Junction, 160, 250, ZoneAId, false),
        new(3, "Lối vào A-01", LayoutNodeType.BoothAccess, 160, 110, ZoneAId, false),
        new(4, "Lối vào A-02", LayoutNodeType.BoothAccess, 360, 110, ZoneAId, false),
        new(5, "Hành lang phía nam", LayoutNodeType.Junction, 160, 390, ZoneAId, false),
        new(6, "Giao lộ khu B", LayoutNodeType.Junction, 360, 250, ZoneBId, false),
        new(7, "Lối vào B-01", LayoutNodeType.BoothAccess, 560, 110, ZoneBId, false),
        new(8, "Lối vào B-02", LayoutNodeType.BoothAccess, 560, 310, ZoneBId, false),
        new(9, "Điểm hẹn sân khấu", LayoutNodeType.Landmark, 360, 390, ZoneBId, true),
        new(10, "Lối vào B-03", LayoutNodeType.BoothAccess, 360, 310, ZoneBId, false)
    ];

    private static readonly EdgeSeed[] Edges =
    [
        new(1, 1, 2, 10), new(2, 2, 3, 14), new(3, 3, 4, 20),
        new(4, 2, 6, 20), new(5, 6, 4, 14), new(6, 4, 7, 20),
        new(7, 6, 10, 6), new(8, 10, 8, 20), new(9, 8, 7, 20),
        new(10, 2, 5, 14), new(11, 5, 9, 20), new(12, 9, 10, 8)
    ];

    private static readonly FoodSeed[][] Menus =
    [
        [new("Ba chỉ nướng sả", 69000), new("Gà nướng lá chanh", 75000), new("Bắp nướng mỡ hành", 32000), new("Trà tắc", 22000)],
        [new("Mực nướng sa tế", 99000), new("Tôm hấp sả", 109000), new("Sò nướng mỡ hành", 79000), new("Nước sâm", 25000)],
        [new("Chè ba màu", 35000), new("Chè khúc bạch", 42000), new("Tàu hũ gừng", 30000), new("Sương sáo hạt é", 32000)],
        [new("Trà đào cam sả", 39000), new("Trà tắc mật ong", 32000), new("Nước mát thảo mộc", 28000), new("Soda chanh", 35000)],
        [new("Bánh tráng nướng", 35000), new("Cá viên chiên", 39000), new("Khoai lang lắc", 32000), new("Xiên que thập cẩm", 45000)]
    ];

    private static readonly short[][] Ratings =
    [
        [5, 5, 5, 5, 4],
        [5, 4],
        [5, 4, 3],
        [4, 3],
        []
    ];

    public static bool IsEnabled(IConfiguration configuration)
        => configuration.GetValue<bool>("SeedDemoData");

    public static async Task<DemoSeedReport> SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SNMDbContext>>();
        IDbContextTransaction? transaction = null;

        try
        {
            logger.LogInformation("Controlled demo seed enabled; transaction starting.");
            if (db.Database.IsRelational())
                transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var roles = await EnsureRolesAsync(db, now, cancellationToken);
            await EnsureUsersAsync(db, roles, hasher, configuration["SeedDemoDataPassword"], now, cancellationToken);
            await EnsureMarketGraphAndBoothsAsync(db, now, cancellationToken);
            await EnsureMenusAsync(db, now, cancellationToken);
            await EnsureReviewHistoryAsync(db, now, cancellationToken);
            await RefreshRatingsAsync(db, now, cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            var report = await BuildReportAsync(db, cancellationToken);
            logger.LogInformation(
                "Phase 03.5 demo seed ready: {Markets} market, {Booths} booths, {Foods} foods, {Reviews} reviews, {Nodes} nodes, {Edges} edges.",
                report.NightMarkets, report.Booths, report.Foods, report.Reviews, report.LayoutNodes, report.LayoutEdges);
            return report;
        }
        catch (Exception exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            logger.LogError(exception, "Controlled demo seed failed; transaction rolled back.");
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static async Task<(Guid Customer, Guid Owner)> EnsureRolesAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        var customer = await db.Roles.SingleOrDefaultAsync(x => x.RoleName == "Customer", ct);
        if (customer is null)
        {
            customer = new Role { Id = CustomerRoleId, RoleName = "Customer", Description = "Khách hàng", CreatedAt = now, UpdatedAt = now };
            db.Roles.Add(customer);
        }

        var owner = await db.Roles.SingleOrDefaultAsync(x => x.RoleName == "BoothOwner", ct);
        if (owner is null)
        {
            owner = new Role { Id = BoothOwnerRoleId, RoleName = "BoothOwner", Description = "Chủ gian hàng", CreatedAt = now, UpdatedAt = now };
            db.Roles.Add(owner);
        }

        await db.SaveChangesAsync(ct);
        return (customer.Id, owner.Id);
    }

    private static async Task EnsureUsersAsync(
        SNMDbContext db,
        (Guid Customer, Guid Owner) roles,
        IPasswordHasher hasher,
        string? configuredPassword,
        DateTime now,
        CancellationToken ct)
    {
        var generatedPassword = string.IsNullOrWhiteSpace(configuredPassword)
            ? Guid.NewGuid().ToString("N")
            : configuredPassword;
        var normalizeConfiguredPassword = !string.IsNullOrWhiteSpace(configuredPassword);

        await AddUserIfMissingAsync(db, CustomerAId, roles.Customer, "phase035.customer.a", "Khách Demo An", "phase035.customer.a@snm.local", generatedPassword, normalizeConfiguredPassword, hasher, now, ct);
        await AddUserIfMissingAsync(db, CustomerBId, roles.Customer, "phase035.customer.b", "Khách Demo Bình", "phase035.customer.b@snm.local", generatedPassword, normalizeConfiguredPassword, hasher, now, ct);
        foreach (var booth in Booths)
            await AddUserIfMissingAsync(db, OwnerId(booth.Index), roles.Owner, $"phase035.owner.{booth.Index:00}", $"Chủ quầy demo {booth.Index:00}", $"phase035.owner.{booth.Index:00}@snm.local", generatedPassword, normalizeConfiguredPassword, hasher, now, ct);

        await db.SaveChangesAsync(ct);
    }

    private static async Task AddUserIfMissingAsync(
        SNMDbContext db, Guid id, Guid roleId, string userName, string fullName, string email,
        string password, bool normalizeConfiguredPassword, IPasswordHasher hasher, DateTime now, CancellationToken ct)
    {
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Demo seed ID collision for user {id}.");
            if (normalizeConfiguredPassword && !string.IsNullOrEmpty(existing.PasswordHash) && !hasher.VerifyPassword(password, existing.PasswordHash))
            {
                existing.PasswordHash = hasher.HashPassword(password);
                existing.UpdatedAt = now;
            }
            return;
        }

        if (await db.Users.AnyAsync(x => x.Email == email || x.UserName == userName, ct))
            throw new InvalidOperationException($"Demo identity {email} already exists with a different ID.");

        db.Users.Add(new User
        {
            Id = id, RoleId = roleId, UserName = userName, FullName = fullName, Email = email,
            PasswordHash = hasher.HashPassword(password), Phone = "0900000350", Address = "TP. Hồ Chí Minh",
            AuthProvider = AuthProvider.Local, Status = UserStatus.Active, CreatedAt = now, UpdatedAt = now
        });
    }

    private static async Task EnsureMarketGraphAndBoothsAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        if (!await db.NightMarkets.AnyAsync(x => x.Id == MarketId, ct))
        {
            db.NightMarkets.Add(new NightMarket
            {
                Id = MarketId, Name = "Smart Night Market Demo", Description = "Không gian trình diễn dữ liệu của đồ án; không đại diện cho một cơ sở kinh doanh có thật.",
                Address = "Khu đô thị Đại học Quốc gia TP.HCM, TP. Thủ Đức, TP. Hồ Chí Minh",
                Latitude = 10.87530m, Longitude = 106.80050m, OpeningHours = new TimeOnly(0, 0), ClosingHours = new TimeOnly(23, 59),
                TotalBooth = Booths.Length, BoundaryWidthMeters = 180, BoundaryHeightMeters = 120,
                ThumbnailUrl = MarketCoverUrl, Status = NightMarketStatus.Active, ModerationStatus = ModerationStatus.Active,
                IsDeleted = false, CreatedAt = now, UpdatedAt = now
            });
        }

        await AddIfMissingAsync(db.NightMarketImages, MarketImageId(1), () => new NightMarketImage { Id = MarketImageId(1), NightMarketId = MarketId, ImageUrl = MarketCoverUrl, DisplayOrder = 1, IsCover = true, CreatedAt = now, UpdatedAt = now });
        await AddIfMissingAsync(db.NightMarketImages, MarketImageId(2), () => new NightMarketImage { Id = MarketImageId(2), NightMarketId = MarketId, ImageUrl = $"{AssetRoot}/market-gallery.svg", DisplayOrder = 2, IsCover = false, CreatedAt = now, UpdatedAt = now });

        // Treat controlled demo seeding as an idempotent media migration as well:
        // rows created by earlier releases must stop serving the generic SVG cover.
        var market = db.NightMarkets.Local.SingleOrDefault(x => x.Id == MarketId)
            ?? await db.NightMarkets.SingleAsync(x => x.Id == MarketId, ct);
        var marketCover = db.NightMarketImages.Local.SingleOrDefault(x => x.Id == MarketImageId(1))
            ?? await db.NightMarketImages.SingleAsync(x => x.Id == MarketImageId(1), ct);
        market.ThumbnailUrl = MarketCoverUrl;
        market.UpdatedAt = now;
        marketCover.ImageUrl = MarketCoverUrl;
        marketCover.UpdatedAt = now;

        await AddIfMissingAsync(db.Zones, ZoneAId, () => new Zone { Id = ZoneAId, NightMarketId = MarketId, ZoneName = "Khu A — Đồ nướng & Hải sản", Description = "Các quầy món nóng ở phía bắc layout.", Color = "#E76F51", Status = ZoneStatus.Active, CreatedAt = now, UpdatedAt = now });
        await AddIfMissingAsync(db.Zones, ZoneBId, () => new Zone { Id = ZoneBId, NightMarketId = MarketId, ZoneName = "Khu B — Tráng miệng & Ăn vặt", Description = "Các quầy đồ uống, món ngọt và ăn vặt.", Color = "#2A9D8F", Status = ZoneStatus.Active, CreatedAt = now, UpdatedAt = now });
        await AddIfMissingAsync(db.MarketLayouts, LayoutId, () => new MarketLayout
        {
            Id = LayoutId, NightMarketId = MarketId, LayoutName = "Mặt bằng demo Phase 03.5", Version = 1,
            LayoutImageUrl = $"{AssetRoot}/layout.svg", Width = 800, Height = 500,
            CoordinateUnit = LayoutCoordinateUnit.LayoutUnit, MetersPerLayoutUnit = 0.1m,
            DistanceCalibrationStatus = DistanceCalibrationStatus.Calibrated,
            GraphRevision = 1,
            Status = MarketLayoutStatus.Active, CreatedAt = now, UpdatedAt = now
        });
        await db.SaveChangesAsync(ct);

        foreach (var node in Nodes)
            await AddIfMissingAsync(db.LayoutNodes, NodeId(node.Index), () => new LayoutNode { Id = NodeId(node.Index), LayoutId = LayoutId, ZoneId = node.ZoneId, NodeName = node.Name, NodeType = node.Type, Xcoordinate = node.X, Ycoordinate = node.Y, IsAccessible = true, IsStartingPoint = node.StartingPoint, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(ct);

        await AddIfMissingAsync(db.LayoutNavigationAnchors, Id("350"), () => new LayoutNavigationAnchor
        {
            Id = Id("350"), LayoutId = LayoutId, LayoutNodeId = MainEntranceNodeId,
            AnchorType = NavigationAnchorType.Entrance, AnchorCode = "MAIN_ENTRANCE",
            DisplayName = "Cổng chính", Latitude = 10.87510m, Longitude = 106.80020m,
            IsCustomerAccessible = true, IsActive = true, OpeningTime = new TimeOnly(0, 0),
            ClosingTime = new TimeOnly(23, 59), CreatedAt = now, UpdatedAt = now
        });
        await db.SaveChangesAsync(ct);

        foreach (var edge in Edges)
            await AddIfMissingAsync(db.LayoutEdges, EdgeId(edge.Index), () => new LayoutEdge { Id = EdgeId(edge.Index), LayoutId = LayoutId, FromNodeId = NodeId(edge.From), ToNodeId = NodeId(edge.To), Distance = edge.Metres, IsBidirectional = true, IsAccessible = true, CreatedAt = now, UpdatedAt = now });

        foreach (var booth in Booths)
        {
            await AddIfMissingAsync(db.Booths, BoothId(booth.Index), () => new Booth
            {
                Id = BoothId(booth.Index), NightMarketId = MarketId,
                BoothOwnerId = OwnerId(booth.Index), ZoneId = booth.ZoneId, BoothName = booth.Name,
                BoothCode = $"DEMO-{booth.Index:00}", Description = booth.Description, PhoneNumber = $"0900035{booth.Index:000}",
                SlotNumber = booth.Slot, ThumbnailUrl = FoodAssetUrl(booth.Index, 1), MapPositionX = booth.X, MapPositionY = booth.Y,
                OpenTime = booth.Open, CloseTime = booth.Close, AverageRating = 0, IsFeatured = booth.Featured,
                PackageName = "Demo Integration", Status = BoothStatus.Active, CreatedAt = now, UpdatedAt = now
            });
            await AddIfMissingAsync(db.BoothImages, BoothImageId(booth.Index, 1), () => new BoothImage { Id = BoothImageId(booth.Index, 1), BoothId = BoothId(booth.Index), ImageUrl = FoodAssetUrl(booth.Index, 1), DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });
            await AddIfMissingAsync(db.BoothImages, BoothImageId(booth.Index, 2), () => new BoothImage { Id = BoothImageId(booth.Index, 2), BoothId = BoothId(booth.Index), ImageUrl = FoodAssetUrl(booth.Index, 2), DisplayOrder = 2, CreatedAt = now, UpdatedAt = now });

            var boothEntity = db.Booths.Local.SingleOrDefault(x => x.Id == BoothId(booth.Index))
                ?? await db.Booths.SingleAsync(x => x.Id == BoothId(booth.Index), ct);
            var boothCover = db.BoothImages.Local.SingleOrDefault(x => x.Id == BoothImageId(booth.Index, 1))
                ?? await db.BoothImages.SingleAsync(x => x.Id == BoothImageId(booth.Index, 1), ct);
            var boothGallery = db.BoothImages.Local.SingleOrDefault(x => x.Id == BoothImageId(booth.Index, 2))
                ?? await db.BoothImages.SingleAsync(x => x.Id == BoothImageId(booth.Index, 2), ct);
            boothEntity.ThumbnailUrl = FoodAssetUrl(booth.Index, 1);
            boothEntity.UpdatedAt = now;
            boothCover.ImageUrl = FoodAssetUrl(booth.Index, 1);
            boothCover.UpdatedAt = now;
            boothGallery.ImageUrl = FoodAssetUrl(booth.Index, 2);
            boothGallery.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        foreach (var booth in Booths)
        {
            var nodeIndex = booth.Index switch { 1 => 3, 2 => 4, 3 => 7, 4 => 8, _ => 10 };
            await AddIfMissingAsync(db.BoothLocations, LocationId(booth.Index), () => new BoothLocation { Id = LocationId(booth.Index), BoothId = BoothId(booth.Index), LayoutId = LayoutId, LayoutNodeId = NodeId(nodeIndex), ZoneId = booth.ZoneId, SlotNumber = booth.Slot, Xcoordinate = booth.X, Ycoordinate = booth.Y, CreatedAt = now, UpdatedAt = now });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureMenusAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        for (var boothIndex = 1; boothIndex <= Booths.Length; boothIndex++)
        {
            await AddIfMissingAsync(db.FoodCategories, CategoryId(boothIndex), () => new FoodCategory { Id = CategoryId(boothIndex), BoothId = BoothId(boothIndex), Name = "Thực đơn demo", Description = "Danh mục dùng cho kiểm thử tích hợp.", CreatedAt = now, UpdatedAt = now });
            var demoCategory = await db.FoodCategories.FindAsync([CategoryId(boothIndex)], ct);
            if (demoCategory is not null)
            {
                demoCategory.Code = $"DEMO_INTEGRATION_{boothIndex:00}";
                demoCategory.IsActive = true;
                demoCategory.IsSelectable = true;
            }
            for (var foodIndex = 1; foodIndex <= Menus[boothIndex - 1].Length; foodIndex++)
            {
                var seed = Menus[boothIndex - 1][foodIndex - 1];
                var capturedBooth = boothIndex;
                var capturedFood = foodIndex;
                var foodAssetUrl = FoodAssetUrl(capturedBooth, capturedFood);
                await AddIfMissingAsync(db.FoodItems, FoodId(capturedBooth, capturedFood), () => new FoodItem { Id = FoodId(capturedBooth, capturedFood), BoothId = BoothId(capturedBooth), CategoryId = CategoryId(capturedBooth), Name = seed.Name, Description = $"{seed.Name} — dữ liệu món ăn demo.", Price = seed.Price, ThumbnailUrl = foodAssetUrl, IsAvailable = !(capturedBooth == 5 && capturedFood == 4), IsFeatured = capturedFood == 1, CreatedAt = now, UpdatedAt = now });
                await AddIfMissingAsync(db.FoodImages, FoodImageId(capturedBooth, capturedFood), () => new FoodImage { Id = FoodImageId(capturedBooth, capturedFood), FoodItemId = FoodId(capturedBooth, capturedFood), ImageUrl = foodAssetUrl, DisplayOrder = 1, CreatedAt = now, UpdatedAt = now });

                // Seed runs are also migrations for controlled demo data: repair
                // legacy rows that all pointed at the same generic food.svg.
                var food = db.FoodItems.Local.SingleOrDefault(x => x.Id == FoodId(capturedBooth, capturedFood))
                    ?? await db.FoodItems.SingleAsync(x => x.Id == FoodId(capturedBooth, capturedFood), ct);
                var image = db.FoodImages.Local.SingleOrDefault(x => x.Id == FoodImageId(capturedBooth, capturedFood))
                    ?? await db.FoodImages.SingleAsync(x => x.Id == FoodImageId(capturedBooth, capturedFood), ct);
                food.ThumbnailUrl = foodAssetUrl;
                image.ImageUrl = foodAssetUrl;
            }
        }
        await db.SaveChangesAsync(ct);

        await EnsureDemoCatalogMetadataAsync(db, now, ct);

        await AddIfMissingAsync(db.FoodPrices, PriceId(1), () => new FoodPrice { Id = PriceId(1), FoodItemId = FoodId(1, 1), Price = 59000, StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureDemoCatalogMetadataAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        for (var boothIndex = 1; boothIndex <= Booths.Length; boothIndex++)
        {
            for (var foodIndex = 1; foodIndex <= Menus[boothIndex - 1].Length; foodIndex++)
            {
                var foodId = FoodId(boothIndex, foodIndex);
                var courseCodes = GetDemoCourseCodes(boothIndex, foodIndex);
                var isDrink = courseCodes.Contains("COURSE_DRINK");
                var isDessert = courseCodes.Contains("COURSE_DESSERT");
                var purposeCode = isDrink ? "PURPOSE_REFRESHMENT"
                    : isDessert ? "PURPOSE_DESSERT"
                    : courseCodes.Contains("COURSE_MAIN_COURSE") ? "PURPOSE_FULL_MEAL"
                    : "PURPOSE_SNACKING";

                var normalizedCourses = courseCodes
                    .Select(code => Enum.Parse<FoodCourse>(code.Replace("COURSE_", "", StringComparison.Ordinal)))
                    .ToHashSet();
                var staleCourses = await db.FoodItemCourses
                    .Where(link => link.FoodItemId == foodId && !normalizedCourses.Contains(link.Course))
                    .ToListAsync(ct);
                db.FoodItemCourses.RemoveRange(staleCourses);
                var existingCourses = await db.FoodItemCourses
                    .Where(link => link.FoodItemId == foodId)
                    .Select(link => link.Course)
                    .ToListAsync(ct);
                foreach (var course in normalizedCourses.Where(course => !existingCourses.Contains(course)))
                    db.FoodItemCourses.Add(new FoodItemCourse
                    {
                        FoodItemId = foodId,
                        Course = course,
                        IsPrimary = normalizedCourses.Count == 1,
                        CreatedAt = now
                    });

                var purpose = Enum.Parse<DiningPurpose>(purposeCode.Replace("PURPOSE_", "", StringComparison.Ordinal));
                if (!await db.FoodItemDiningPurposes.AnyAsync(link => link.FoodItemId == foodId && link.Purpose == purpose, ct))
                    db.FoodItemDiningPurposes.Add(new FoodItemDiningPurpose { FoodItemId = foodId, Purpose = purpose, CreatedAt = now });

                var food = await db.FoodItems.SingleAsync(item => item.Id == foodId, ct);
                food.ServingTemperature = isDrink || isDessert ? ServingTemperature.COLD : ServingTemperature.HOT;
                food.SemanticProfileVersion = Math.Max(food.SemanticProfileVersion, 1);
                food.SemanticProfileUpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyCollection<string> GetDemoCourseCodes(int boothIndex, int foodIndex)
        => boothIndex switch
        {
            1 => foodIndex switch
            {
                1 or 2 => ["COURSE_MAIN_COURSE"],
                3 => ["COURSE_APPETIZER", "COURSE_SIDE_DISH"],
                _ => ["COURSE_DRINK"]
            },
            2 => foodIndex switch
            {
                1 or 2 => ["COURSE_MAIN_COURSE"],
                3 => ["COURSE_APPETIZER", "COURSE_SHARED_DISH"],
                _ => ["COURSE_DRINK"]
            },
            3 => ["COURSE_DESSERT"],
            4 => ["COURSE_DRINK"],
            5 => foodIndex == 4
                ? ["COURSE_SHARED_DISH"]
                : ["COURSE_APPETIZER", "COURSE_SIDE_DISH"],
            _ => ["COURSE_EXTRA"]
        };

    private static async Task EnsureReviewHistoryAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        var sequence = 1;
        for (var boothIndex = 1; boothIndex <= Ratings.Length; boothIndex++)
        {
            foreach (var rating in Ratings[boothIndex - 1])
            {
                var customerId = sequence % 2 == 0 ? CustomerAId : CustomerBId;
                var orderId = OrderId(sequence);
                var amount = Menus[boothIndex - 1][0].Price;
                var occurredAt = now.AddDays(-sequence);
                await AddIfMissingAsync(db.Orders, orderId, () => new Order { Id = orderId, CustomerId = customerId, BoothOwnerId = OwnerId(boothIndex), OrderCode = 935000000 + sequence, CheckoutRequestId = CheckoutId(sequence), Status = OrderStatus.Completed, TotalAmount = amount, DiscountAmount = 0, FinalAmount = amount, Note = "Đơn hoàn tất phục vụ review demo Phase 03.5", CreatedAt = occurredAt, UpdatedAt = occurredAt });
                await AddIfMissingAsync(db.OrderDetails, OrderDetailId(sequence), () => new OrderDetail { Id = OrderDetailId(sequence), OrderId = orderId, FoodItemId = FoodId(boothIndex, 1), FoodNameSnapshot = Menus[boothIndex - 1][0].Name, Quantity = 1, UnitPrice = amount, TotalPrice = amount, CreatedAt = occurredAt, UpdatedAt = occurredAt });
                await AddIfMissingAsync(db.Payments, PaymentId(sequence), () => new Payment { Id = PaymentId(sequence), OrderId = orderId, BoothOwnerId = OwnerId(boothIndex), Type = PaymentType.Cash, Gateway = PaymentGateway.None, Amount = amount, Status = PaymentStatus.Paid, PaidAt = occurredAt, CreatedAt = occurredAt, UpdatedAt = occurredAt });
                await AddIfMissingAsync(db.Reviews, ReviewId(sequence), () => new Review { Id = ReviewId(sequence), BoothId = BoothId(boothIndex), CustomerId = customerId, OrderId = orderId, Rating = rating, Content = $"Đánh giá demo hợp lệ cho quầy {Booths[boothIndex - 1].Name}.", ImageUrl = sequence == 1 ? FoodAssetUrl(1, 1) : null, IsVisible = true, CreatedAt = occurredAt.AddHours(1), UpdatedAt = occurredAt.AddHours(1) });
                if (sequence == 1)
                {
                    var review = db.Reviews.Local.SingleOrDefault(x => x.Id == ReviewId(sequence))
                        ?? await db.Reviews.SingleAsync(x => x.Id == ReviewId(sequence), ct);
                    review.ImageUrl = FoodAssetUrl(1, 1);
                    review.UpdatedAt = now;
                }
                sequence++;
            }
        }
        await db.SaveChangesAsync(ct);

        await AddIfMissingAsync(db.ReviewReplies, ReplyId(1), () => new ReviewReply { Id = ReplyId(1), ReviewId = ReviewId(1), BoothOwnerId = OwnerId(1), Content = "Cảm ơn bạn đã góp ý. Hẹn gặp lại tại quầy demo!", CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) });
        await db.SaveChangesAsync(ct);
    }

    private static async Task RefreshRatingsAsync(SNMDbContext db, DateTime now, CancellationToken ct)
    {
        foreach (var booth in Booths)
        {
            var ratings = await db.Reviews.Where(x => x.BoothId == BoothId(booth.Index) && x.IsVisible).Select(x => x.Rating).ToListAsync(ct);
            var entity = await db.Booths.SingleAsync(x => x.Id == BoothId(booth.Index), ct);
            entity.AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(x => (decimal)x), 2);
            entity.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<DemoSeedReport> BuildReportAsync(SNMDbContext db, CancellationToken ct)
        => new(
            await db.NightMarkets.CountAsync(x => x.Id == MarketId, ct),
            await db.Booths.CountAsync(x => x.NightMarketId == MarketId, ct),
            await db.FoodItems.CountAsync(x => x.Booth.NightMarketId == MarketId, ct),
            await db.Reviews.CountAsync(x => x.Booth.NightMarketId == MarketId, ct),
            await db.Zones.CountAsync(x => x.NightMarketId == MarketId, ct),
            await db.MarketLayouts.CountAsync(x => x.NightMarketId == MarketId, ct),
            await db.LayoutNodes.CountAsync(x => x.LayoutId == LayoutId, ct),
            await db.LayoutEdges.CountAsync(x => x.LayoutId == LayoutId, ct),
            await db.LayoutNodes.CountAsync(x => x.LayoutId == LayoutId && x.IsStartingPoint, ct),
            await db.BoothLocations.CountAsync(x => x.LayoutId == LayoutId, ct));

    private static async Task AddIfMissingAsync<TEntity>(DbSet<TEntity> set, Guid id, Func<TEntity> factory) where TEntity : class
    {
        if (await set.FindAsync(id) is null)
            set.Add(factory());
    }

    private static Guid Id(string suffix) => Guid.Parse($"d3500000-0000-0000-0000-{suffix.PadLeft(12, '0')}");
    private static string FoodAssetUrl(int boothIndex, int foodIndex)
        => $"{AssetRoot}/food-{boothIndex}-{foodIndex}-v2.jpg";
    public static Guid BoothId(int i) => Id($"4{i:00}");
    public static Guid FoodId(int booth, int food) => Id($"5{booth:00}{food:00}");
    public static Guid NodeId(int i) => Id($"3{i:00}");
    private static Guid OwnerId(int i) => Id($"1{i:02}");
    private static Guid LocationId(int i) => Id($"4{i:02}2");
    private static Guid CategoryId(int i) => Id($"5{i:02}0");
    private static Guid EdgeId(int i) => Id($"6{i:02}");
    private static Guid MarketImageId(int i) => Id($"10{i}");
    private static Guid BoothImageId(int booth, int image) => Id($"7{booth:02}{image:02}");
    private static Guid FoodImageId(int booth, int food) => Id($"8{booth:02}{food:02}");
    private static Guid PriceId(int i) => Id($"90{i}");
    private static Guid OrderId(int i) => Id($"91{i:02}");
    private static Guid CheckoutId(int i) => Id($"92{i:02}");
    private static Guid OrderDetailId(int i) => Id($"93{i:02}");
    private static Guid PaymentId(int i) => Id($"94{i:02}");
    private static Guid ReviewId(int i) => Id($"95{i:02}");
    private static Guid ReplyId(int i) => Id($"96{i:02}");

    private sealed record BoothSeed(int Index, string Name, string Description, string Slot, Guid ZoneId, decimal X, decimal Y, bool Featured, TimeOnly Open, TimeOnly Close);
    private sealed record NodeSeed(int Index, string Name, LayoutNodeType Type, decimal X, decimal Y, Guid? ZoneId, bool StartingPoint);
    private sealed record EdgeSeed(int Index, int From, int To, decimal Metres);
    private sealed record FoodSeed(string Name, decimal Price);
}

public sealed record DemoSeedReport(
    int NightMarkets,
    int Booths,
    int Foods,
    int Reviews,
    int Zones,
    int MarketLayouts,
    int LayoutNodes,
    int LayoutEdges,
    int StartingPoints,
    int BoothLocations);
