using System.Security.Cryptography;
using System.Text;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260804023000_AddCuratedVietnameseNightMarketMenus")]
public sealed class AddCuratedVietnameseNightMarketMenus : Migration
{
    private static readonly Guid MarketId = Guid.Parse("a2600000-0000-0000-0000-000000000001");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO "Role" ("Id", "RoleName", "Description", "CreatedAt", "UpdatedAt")
            VALUES ('a2600000-0000-0000-0000-000000000002', 'BoothOwner', 'Chủ gian hàng', now(), now())
            ON CONFLICT ("RoleName") DO NOTHING;

            INSERT INTO "NightMarket" ("Id", "Name", "Description", "Address", "Latitude", "Longitude",
                "OpeningHours", "ClosingHours", "TotalBooth", "Status", "ModerationStatus", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES ('a2600000-0000-0000-0000-000000000001', 'Phố Ẩm Thực Đêm Việt',
                'Khu ẩm thực Việt Nam được tổ chức theo từng quầy chuyên món để phục vụ tìm kiếm, gọi món và gợi ý thực đơn.',
                'Khu đô thị Đại học Quốc gia TP.HCM, TP. Thủ Đức, TP. Hồ Chí Minh', 10.8753000, 106.8005000,
                TIME '00:00', TIME '23:59', 10, 'Active', 'Active', false, now(), now())
            ON CONFLICT ("Id") DO NOTHING;
            """);

        for (var boothIndex = 1; boothIndex <= Booths.Length; boothIndex++)
        {
            var booth = Booths[boothIndex - 1];
            var ownerId = Stable("a2610000", boothIndex);
            var registrationId = Stable("a2620000", boothIndex);
            var boothId = Stable("a2630000", boothIndex);
            var categoryId = Stable("a2640000", boothIndex);
            var code = $"CURATED_VN_{boothIndex:00}";
            var userName = $"curated.menu.owner.{boothIndex:00}";
            var email = $"curated.menu.owner.{boothIndex:00}@snm.local";

            migrationBuilder.Sql($$"""
                INSERT INTO "User" ("Id", "RoleId", "UserName", "PasswordHash", "FullName", "Email", "AuthProvider", "Status", "CreatedAt", "UpdatedAt")
                SELECT '{{ownerId}}', r."Id", '{{userName}}', 'MIGRATION_MANAGED_NON_LOGIN_ACCOUNT', '{{Sql(booth.Name)}}', '{{email}}', 'Local', 'Active', now(), now()
                FROM "Role" r WHERE r."RoleName" = 'BoothOwner'
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "BoothRegistrations" ("Id", "OwnerId", "RequestedNightMarketId", "BoothName", "Description", "Status", "CreatedAt", "UpdatedAt")
                VALUES ('{{registrationId}}', '{{ownerId}}', '{{MarketId}}', '{{Sql(booth.Name)}}', '{{Sql(booth.Description)}}', 2, now(), now())
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "Booth" ("Id", "RegistrationId", "NightMarketId", "BoothOwnerId", "BoothName", "BoothCode", "Description",
                    "SlotNumber", "OpenTime", "CloseTime", "Status", "IsFeatured", "CreatedAt", "UpdatedAt")
                VALUES ('{{boothId}}', '{{registrationId}}', '{{MarketId}}', '{{ownerId}}', '{{Sql(booth.Name)}}', '{{code}}',
                    '{{Sql(booth.Description)}}', 'V-{{boothIndex:00}}', TIME '00:00', TIME '23:59', 'Active', {{(boothIndex <= 3 ? "true" : "false")}}, now(), now())
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "FoodCategories" ("Id", "BoothId", "Code", "Name", "Description", "DisplayOrder", "IsSystem", "IsActive", "IsSelectable", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES ('{{categoryId}}', '{{boothId}}', '{{code}}', '{{Sql(booth.Category)}}', '{{Sql(booth.Description)}}', 1, false, true, true, false, now(), now())
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }

        EnsureSemanticCatalogs(migrationBuilder);

        for (var index = 0; index < Foods.Length; index++)
        {
            var food = Foods[index];
            var foodId = Stable("a2650000", index + 1);
            var imageId = Stable("a2660000", index + 1);
            var boothId = Stable("a2630000", food.Booth);
            var categoryId = Stable("a2640000", food.Booth);
            var imageUrl = CommonsImage(food.FileName);
            var description = $"{food.Name} ({food.EnglishName}) — món thật trong thực đơn {Booths[food.Booth - 1].Category.ToLowerInvariant()}, dùng nguyên liệu và cách chế biến đúng tên món.";
            var searchText = BuildSearchText(food, description);
            var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(searchText)));
            var course = Course(food.Booth);
            var purpose = Purpose(food.Booth);
            var temperature = Temperature(food.Booth);
            var spice = Spice(food.Name);
            var shareable = food.Booth is 4 or 10;
            var servings = shareable ? 2 : 1;

            migrationBuilder.InsertData(
                table: "FoodItem",
                columns: ["Id", "BoothId", "CategoryId", "Name", "Description", "Price", "ThumbnailUrl", "IsAvailable", "IsFeatured", "IsDeleted", "CreatedAt", "UpdatedAt", "SpiceLevel", "ServingTemperature", "EstimatedServingCount", "ServingSizeDescription", "IsShareable", "SemanticProfileVersion", "SemanticProfileUpdatedAt"],
                columnTypes: ["uuid", "uuid", "uuid", "character varying(200)", "text", "numeric(12,2)", "character varying(500)", "boolean", "boolean", "boolean", "timestamp with time zone", "timestamp with time zone", "character varying(20)", "character varying(20)", "integer", "character varying(300)", "boolean", "integer", "timestamp with time zone"],
                values: [foodId, boothId, categoryId, food.Name, description, food.Price, imageUrl, true, index % 10 == 0, false, SeededAt, SeededAt, spice, temperature, servings, shareable ? "Phần dùng chung 2 người" : "Một phần", shareable, 2, SeededAt]);

            migrationBuilder.InsertData(
                table: "FoodImages",
                columns: ["Id", "FoodItemId", "ImageUrl", "DisplayOrder", "CreatedAt", "UpdatedAt"],
                columnTypes: ["uuid", "uuid", "character varying(500)", "integer", "timestamp with time zone", "timestamp with time zone"],
                values: [imageId, foodId, imageUrl, 1, SeededAt, SeededAt]);

            migrationBuilder.InsertData(
                table: "FoodAiProfile",
                columns: ["FoodItemId", "SearchText", "ContentHash", "Status", "Version", "CreatedAt", "UpdatedAt"],
                columnTypes: ["uuid", "text", "character varying(64)", "character varying(20)", "integer", "timestamp with time zone", "timestamp with time zone"],
                values: [foodId, searchText, contentHash, "READY", 2, SeededAt, SeededAt]);

            migrationBuilder.InsertData(
                table: "FoodItemCourse",
                columns: ["FoodItemId", "Course", "IsPrimary", "CreatedAt"],
                columnTypes: ["uuid", "character varying(30)", "boolean", "timestamp with time zone"],
                values: [foodId, course, true, SeededAt]);

            migrationBuilder.InsertData(
                table: "FoodItemDiningPurpose",
                columns: ["FoodItemId", "Purpose", "CreatedAt"],
                columnTypes: ["uuid", "character varying(30)", "timestamp with time zone"],
                values: [foodId, purpose, SeededAt]);

            InsertCatalogRelation(migrationBuilder, "FoodItemIngredient", "Ingredient", "IngredientId", Ingredient(food), foodId, "\"IsPrimary\", \"IsOptional\"", "true, false");
            InsertCatalogRelation(migrationBuilder, "FoodItemPreparationMethod", "PreparationMethod", "PreparationMethodId", Method(food), foodId, "\"IsPrimary\"", "true");
            InsertCatalogRelation(migrationBuilder, "FoodItemTasteProfile", "TasteProfile", "TasteProfileId", Taste(food.Booth), foodId, "\"Intensity\"", food.Booth is 7 or 8 ? "4" : "3");

            if (food.Booth == 9)
            {
                migrationBuilder.Sql($$"""
                    INSERT INTO "FoodItemDietaryAttribute" ("FoodItemId", "DietaryAttributeId", "SuitabilityStatus", "IsConfirmed", "Source", "CreatedAt", "UpdatedAt")
                    SELECT '{{foodId}}', "Id", 'SUITABLE', true, 'ADMIN_VERIFIED', '{{SeededAt:O}}', '{{SeededAt:O}}'
                    FROM "DietaryAttribute" WHERE "Code" = 'VEGETARIAN'
                    ON CONFLICT DO NOTHING;
                    """);
            }
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "Booth" WHERE "Id"::text LIKE 'a2630000-0000-0000-0000-%';
            DELETE FROM "BoothRegistrations" WHERE "Id"::text LIKE 'a2620000-0000-0000-0000-%';
            DELETE FROM "User" WHERE "Id"::text LIKE 'a2610000-0000-0000-0000-%';
            DELETE FROM "NightMarket" WHERE "Id" = 'a2600000-0000-0000-0000-000000000001';
            """);
    }

    private static void EnsureSemanticCatalogs(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO "Ingredient" ("Id", "Code", "Name", "NormalizedName", "IsSystem", "IsActive", "DisplayOrder", "CreatedAt", "UpdatedAt") VALUES
              ('a2670000-0000-0000-0000-000000000001','BEEF','Thịt bò','thit bo',true,true,1,now(),now()),
              ('a2670000-0000-0000-0000-000000000002','PORK','Thịt heo','thit heo',true,true,2,now(),now()),
              ('a2670000-0000-0000-0000-000000000003','CHICKEN','Thịt gà','thit ga',true,true,3,now(),now()),
              ('a2670000-0000-0000-0000-000000000004','SEAFOOD','Hải sản','hai san',true,true,4,now(),now()),
              ('a2670000-0000-0000-0000-000000000005','VEGETABLE','Rau củ','rau cu',true,true,5,now(),now()),
              ('a2670000-0000-0000-0000-000000000006','FRUIT','Trái cây','trai cay',true,true,6,now(),now()),
              ('a2670000-0000-0000-0000-000000000007','COFFEE_TEA','Cà phê và trà','ca phe va tra',true,true,7,now(),now()),
              ('a2670000-0000-0000-0000-000000000008','RICE_FLOUR','Gạo và bột gạo','gao va bot gao',true,true,8,now(),now())
            ON CONFLICT ("Code") DO NOTHING;

            INSERT INTO "PreparationMethod" ("Id", "Code", "Name", "IsSystem", "IsActive", "DisplayOrder", "CreatedAt", "UpdatedAt") VALUES
              ('a2680000-0000-0000-0000-000000000001','SOUP_COOKED','Nấu nước',true,true,1,now(),now()),
              ('a2680000-0000-0000-0000-000000000002','GRILLED','Nướng',true,true,2,now(),now()),
              ('a2680000-0000-0000-0000-000000000003','FRIED','Chiên',true,true,3,now(),now()),
              ('a2680000-0000-0000-0000-000000000004','STEAMED','Hấp',true,true,4,now(),now()),
              ('a2680000-0000-0000-0000-000000000005','STIR_FRIED','Xào',true,true,5,now(),now()),
              ('a2680000-0000-0000-0000-000000000006','MIXED','Trộn',true,true,6,now(),now()),
              ('a2680000-0000-0000-0000-000000000007','BREWED','Pha chế',true,true,7,now(),now())
            ON CONFLICT ("Code") DO NOTHING;

            INSERT INTO "TasteProfile" ("Id", "Code", "Name", "IsSystem", "IsActive", "DisplayOrder", "CreatedAt", "UpdatedAt") VALUES
              ('a2690000-0000-0000-0000-000000000001','SAVORY','Đậm đà',true,true,1,now(),now()),
              ('a2690000-0000-0000-0000-000000000002','SWEET','Ngọt',true,true,2,now(),now()),
              ('a2690000-0000-0000-0000-000000000003','REFRESHING','Thanh mát',true,true,3,now(),now()),
              ('a2690000-0000-0000-0000-000000000004','LIGHT','Thanh nhẹ',true,true,4,now(),now())
            ON CONFLICT ("Code") DO NOTHING;

            INSERT INTO "DietaryAttribute" ("Id", "Code", "Name", "IsSystem", "IsActive", "DisplayOrder", "CreatedAt", "UpdatedAt")
            VALUES ('a26a0000-0000-0000-0000-000000000001','VEGETARIAN','Món chay',true,true,1,now(),now())
            ON CONFLICT ("Code") DO NOTHING;
            """);
    }

    private static void InsertCatalogRelation(MigrationBuilder migrationBuilder, string joinTable, string catalogTable, string fkColumn, string code, Guid foodId, string extraColumns, string extraValues)
        => migrationBuilder.Sql($$"""
            INSERT INTO "{{joinTable}}" ("FoodItemId", "{{fkColumn}}", {{extraColumns}}, "CreatedAt")
            SELECT '{{foodId}}', "Id", {{extraValues}}, '{{SeededAt:O}}' FROM "{{catalogTable}}" WHERE "Code" = '{{code}}'
            ON CONFLICT DO NOTHING;
            """);

    private static string BuildSearchText(FoodSeed food, string description)
        => string.Join(' ', new[] { food.Name, food.EnglishName, description, Booths[food.Booth - 1].SearchAliases, Course(food.Booth), Purpose(food.Booth) }).ToLowerInvariant();

    private static string Ingredient(FoodSeed food)
    {
        var name = food.Name.ToLowerInvariant();
        if (food.Booth == 9) return "VEGETABLE";
        if (food.Booth == 10 || name.Contains("hải sản") || name.Contains("tôm") || name.Contains("mực") || name.Contains("cua") || name.Contains("hàu") || name.Contains("nghêu") || name.Contains("sò") || name.Contains("bạch tuộc")) return "SEAFOOD";
        if (name.Contains("gà")) return "CHICKEN";
        if (name.Contains("bò") || name.Contains("phở")) return "BEEF";
        if (food.Booth == 7) return "FRUIT";
        if (food.Booth == 8) return "COFFEE_TEA";
        if (food.Booth is 1 or 2 or 3 or 6) return "RICE_FLOUR";
        return "PORK";
    }

    private static string Method(FoodSeed food)
    {
        var name = food.Name.ToLowerInvariant();
        if (food.Booth is 1 or 2 || name.Contains("phở") || name.Contains("bún huế")) return "SOUP_COOKED";
        if (food.Booth == 4 || name.Contains("nướng")) return "GRILLED";
        if (name.Contains("chiên") || name.Contains("viên") || name.Contains("khoai")) return "FRIED";
        if (name.Contains("hấp") || name.Contains("cuốn") || name.Contains("bao")) return "STEAMED";
        if (name.Contains("xào") || food.Booth == 3) return "STIR_FRIED";
        if (food.Booth == 8) return "BREWED";
        return "MIXED";
    }

    private static string Taste(int booth) => booth switch { 7 => "SWEET", 8 => "REFRESHING", 9 => "LIGHT", _ => "SAVORY" };
    private static string Course(int booth) => booth switch { 5 or 6 => "APPETIZER", 7 => "DESSERT", 8 => "DRINK", 4 or 10 => "SHARED_DISH", _ => "MAIN_COURSE" };
    private static string Purpose(int booth) => booth switch { 5 or 6 => "SNACKING", 7 => "DESSERT", 8 => "REFRESHMENT", 4 or 10 => "SHARING", 9 => "LIGHT_MEAL", _ => "FULL_MEAL" };
    private static string Temperature(int booth) => booth switch { 7 or 8 => "COLD", _ => "HOT" };
    private static string Spice(string name) => name.Contains("cay", StringComparison.OrdinalIgnoreCase) || name.Contains("sa tế", StringComparison.OrdinalIgnoreCase) || name.Contains("muối ớt", StringComparison.OrdinalIgnoreCase) ? "MILD" : "NON_SPICY";
    private static string CommonsImage(string fileName) => $"https://commons.wikimedia.org/wiki/Special:Redirect/file/{Uri.EscapeDataString(fileName)}";
    private static string Sql(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static Guid Stable(string prefix, int value) => Guid.Parse($"{prefix}-0000-0000-0000-{value:000000000000}");
    private static readonly DateTime SeededAt = new(2026, 8, 4, 2, 30, 0, DateTimeKind.Utc);

    private static readonly BoothSeed[] Booths =
    [
        new("Phở Việt Bốn Mùa", "Phở", "Phở bò và phở gà nấu nước dùng trong, phục vụ từng tô.", "phở pho noodle soup hot filling meal"),
        new("Bún Mì Ba Miền", "Bún, mì và hủ tiếu", "Các món bún, mì và hủ tiếu đặc trưng ba miền.", "bún mì noodles soup full meal"),
        new("Cơm Nhà Việt", "Cơm", "Cơm phần Việt Nam với thịt, gà, bò và hải sản.", "cơm rice filling meal main course"),
        new("Bếp Than Phố Việt", "Món nướng", "Món nướng nóng chế biến theo phần dùng chung.", "grilled nướng hot sharing friends"),
        new("Góc Ăn Vặt Sài Gòn", "Ăn vặt", "Món ăn vặt đường phố chế biến tại quầy.", "snack street food quick meal"),
        new("Bánh Việt & Món Cuốn", "Bánh và món cuốn", "Bánh mặn, bánh mì và món cuốn Việt Nam.", "bánh bread rolls appetizer takeaway"),
        new("Chè Ngọt Lành", "Chè và tráng miệng", "Chè và tráng miệng Việt dùng lạnh hoặc ấm.", "chè dessert sweet cold refreshing"),
        new("Trạm Nước Mát Việt", "Đồ uống", "Trà, cà phê và nước trái cây pha tại quầy.", "drink beverage refreshing cold coffee tea juice"),
        new("Bếp Chay An Nhiên", "Món chay", "Món chay từ rau, nấm, đậu hũ và ngũ cốc.", "vegetarian vegan light meal no meat"),
        new("Hải Sản Gió Biển", "Hải sản", "Hải sản hấp và nướng, phù hợp dùng chung.", "seafood grilled steamed sharing friends")
    ];

    private static FoodSeed[] BuildFoods()
    {
        var groups = new (string Name, string English, decimal Price)[][]
        {
            [
                ("Phở bò tái", "Rare beef pho", 65000), ("Phở bò viên", "Beef meatball pho", 65000), ("Phở bò tái nạm", "Rare beef and brisket pho", 75000),
                ("Phở gà", "Chicken pho", 60000), ("Phở đặc biệt", "Special combination pho", 85000), ("Phở chay", "Vegetarian pho", 55000),
                ("Phở bò sốt vang", "Beef stew pho", 80000), ("Phở bò gầu", "Beef brisket pho", 75000), ("Phở bò tái lăn", "Wok-seared beef pho", 80000), ("Phở thố đá", "Hot stone bowl pho", 95000)
            ],
            [
                ("Bún bò Huế", "Hue beef noodle soup", 70000), ("Bún thịt nướng", "Grilled pork vermicelli", 60000), ("Bún riêu cua", "Crab tomato noodle soup", 65000),
                ("Bún chả", "Hanoi grilled pork vermicelli", 70000), ("Mì Quảng", "Quang noodles", 65000), ("Hủ tiếu Nam Vang", "Phnom Penh noodle soup", 70000),
                ("Mì xào bò", "Beef stir-fried noodles", 70000), ("Miến gà", "Chicken glass noodle soup", 60000), ("Hủ tiếu khô", "Dry mixed hu tieu", 65000), ("Bún mắm", "Fermented fish noodle soup", 75000)
            ],
            [
                ("Cơm tấm sườn", "Broken rice with pork chop", 65000), ("Cơm gà", "Vietnamese chicken rice", 60000), ("Cơm chiên hải sản", "Seafood fried rice", 75000),
                ("Cơm bò lúc lắc", "Shaking beef rice", 85000), ("Cơm chay", "Vegetarian rice plate", 50000), ("Cơm sườn bì chả", "Broken rice with pork chop, skin and egg loaf", 80000),
                ("Cơm gà xối mỡ", "Crispy chicken rice", 70000), ("Cơm hến", "Hue baby clam rice", 55000), ("Cơm chiên dương châu", "Yangzhou fried rice", 70000), ("Cơm thịt kho trứng", "Braised pork and egg rice", 65000)
            ],
            [
                ("Ba chỉ nướng sả", "Lemongrass grilled pork belly", 90000), ("Bò cuốn nấm", "Grilled beef mushroom rolls", 110000), ("Gà nướng lá chanh", "Kaffir lime leaf grilled chicken", 120000),
                ("Mực nướng sa tế", "Satay grilled squid", 140000), ("Tôm nướng muối ớt", "Chili salt grilled shrimp", 160000), ("Hàu nướng mỡ hành", "Scallion oil grilled oysters", 120000),
                ("Bạch tuộc nướng", "Grilled octopus", 150000), ("Xiên que thập cẩm", "Mixed grilled skewers", 100000), ("Bò nướng lá lốt", "Betel leaf grilled beef", 100000), ("Cánh gà nướng", "Grilled chicken wings", 90000)
            ],
            [
                ("Bánh tráng trộn", "Mixed rice paper salad", 35000), ("Bánh tráng nướng", "Grilled rice paper", 40000), ("Cá viên chiên", "Fried fish balls", 40000),
                ("Khoai tây lắc", "Seasoned shaken fries", 35000), ("Phô mai que", "Fried cheese sticks", 45000), ("Gà viên", "Fried chicken bites", 45000),
                ("Xúc xích nướng", "Grilled sausage", 40000), ("Bắp xào", "Stir-fried corn", 35000), ("Trứng cút xào me", "Tamarind quail eggs", 45000), ("Khoai lang kén", "Crispy sweet potato croquettes", 40000)
            ],
            [
                ("Bánh xèo", "Vietnamese sizzling pancake", 70000), ("Bánh khọt", "Mini savory coconut pancakes", 60000), ("Bánh cuốn", "Steamed rice rolls", 50000),
                ("Bánh bao", "Steamed stuffed bun", 35000), ("Bánh mì thịt", "Vietnamese pork sandwich", 45000), ("Bánh mì chảo", "Vietnamese skillet breakfast", 75000),
                ("Gỏi cuốn", "Fresh spring rolls", 50000), ("Chả giò", "Fried spring rolls", 55000), ("Bánh bèo", "Steamed rice cakes", 50000), ("Bánh bột lọc", "Tapioca dumplings", 55000)
            ],
            [
                ("Chè ba màu", "Three-colour sweet soup", 35000), ("Chè khúc bạch", "Almond panna cotta sweet soup", 45000), ("Chè Thái", "Thai-style fruit sweet soup", 45000),
                ("Chè đậu đen", "Black bean sweet soup", 30000), ("Sương sáo hạt é", "Grass jelly with basil seeds", 35000), ("Tàu hũ nước đường", "Silken tofu in ginger syrup", 30000),
                ("Sữa chua trái cây", "Fruit yogurt", 45000), ("Trái cây dầm", "Mixed fruit cup", 50000), ("Kem dừa", "Coconut ice cream", 50000), ("Chè bắp", "Sweet corn pudding", 35000)
            ],
            [
                ("Trà tắc", "Kumquat tea", 25000), ("Trà đào", "Peach tea", 35000), ("Trà chanh", "Lemon tea", 25000), ("Trà sữa trân châu", "Bubble milk tea", 45000),
                ("Nước cam", "Orange juice", 40000), ("Nước ép dưa hấu", "Watermelon juice", 40000), ("Nước ép ổi", "Guava juice", 40000), ("Nước sâm", "Herbal cooling drink", 25000),
                ("Cà phê sữa đá", "Vietnamese iced milk coffee", 35000), ("Bạc xỉu", "Vietnamese white coffee", 40000)
            ],
            [
                ("Phở chay nấm", "Vegetarian mushroom pho", 60000), ("Bún Huế chay", "Vegetarian Hue noodle soup", 60000), ("Cơm chay thập cẩm", "Mixed vegetarian rice", 55000),
                ("Gỏi cuốn chay", "Vegetarian fresh spring rolls", 50000), ("Mì xào rau củ", "Vegetable stir-fried noodles", 60000), ("Đậu hũ sốt nấm", "Tofu in mushroom sauce", 65000),
                ("Chả giò chay", "Vegetarian fried spring rolls", 50000), ("Mì Quảng chay", "Vegetarian Quang noodles", 60000), ("Bún riêu chay", "Vegetarian tomato noodle soup", 60000), ("Cà ri chay", "Vegetarian curry", 65000)
            ],
            [
                ("Tôm hấp sả", "Lemongrass steamed shrimp", 160000), ("Tôm nướng", "Grilled shrimp", 170000), ("Mực hấp gừng", "Ginger steamed squid", 140000), ("Mực nướng", "Grilled squid", 150000),
                ("Nghêu hấp sả", "Lemongrass steamed clams", 100000), ("Sò điệp nướng", "Grilled scallops", 160000), ("Hàu nướng", "Grilled oysters", 130000), ("Bạch tuộc nướng sa tế", "Satay grilled octopus", 170000),
                ("Ốc hương xào bơ tỏi", "Garlic butter sea snails", 180000), ("Cua rang me", "Tamarind crab", 250000)
            ]
        };

        var images = ImageFiles;
        var result = new List<FoodSeed>(100);
        var imageIndex = 0;
        for (var booth = 1; booth <= groups.Length; booth++)
        foreach (var item in groups[booth - 1])
            result.Add(new FoodSeed(booth, item.Name, item.English, item.Price, images[imageIndex++]));
        return result.ToArray();
    }

    private static readonly string[] ImageFiles =
    [
        "Tô phở bò tái ở Q1 (phở 24) ng27th4n2024 (1).jpg", "Vietnamese Pho.jpg", "Phở đặc biệt.jpg", "Tofu noodle soup (Phở chay) - Pho Hanoi Authentic 2024-12-01.jpg",
        "Bữa ăn sáng tại Quán Đôi Dép ở Q1 (tô phở thố đá đông trùng hạ thảo và cà phê sữa nóng) (1).jpg", "Bữa ăn sáng tại Quán Đôi Dép ở Q1 (tô phở thố đá đông trùng hạ thảo và cà phê sữa nóng) (2).jpg", "Công viên Cọ Dầu ở Đông Hà dịp Lễ 2th9n2023 (phở Lý Quốc sư) (1).jpg", "Công viên Cọ Dầu ở Đông Hà dịp Lễ 2th9n2023 (phở Lý Quốc sư) (2).jpg", "Công viên Cọ Dầu ở Đông Hà dịp Lễ 2th9n2023 (phở Lý Quốc sư) (3).jpg", "Guests adjust the spice before enjoying phở 15-07-2018.jpg",
        "Bún bò Huế minh28397.jpg", "Bun thit nuong.jpg", "Bún riêu cua nước.jpg", "Bún chả Hàng Mành.jpg", "Mi Quang 1A Danang.jpg", "Hu Tieu Nam Vang.jpg", "Cà phê Nguyệt Ca (món mì xào) tháng 6 năm 2016 (2).jpg", "Vùng Cùa ở Cam Lộ th5n2023-Phương Gia Trang (tô miến gà) (2).jpg", "Hủ tiếu khô.jpg", "Bún Mắm Sóc Trăng.jpg",
        "Cơm tấm sườn cây.JPG", "AE liên hoan, Cơm gà Trúc My ơi, ng1th9n2019 (gà luộc) (3).jpg", "Prawn fried rice shrimp bowls.jpg", "Bo luc lac.jpg", "Cam Lộ th5n2023 (quán cơm chay cạnh chùa Cam Lộ) (1).jpg", "Cơm tấm sườn chả (chả trứng) tại quán cơm bình dân đường NS ng25th9n2022 (dĩa cơm với chả trứng) (2).jpg", "Bogus Chicken rice.jpg", "Com Hen at Hue Vietnam.JPG", "Yangzhou fried rice and drinks 06-09-2019.jpg", "Vietnamese thit kho, braised pork belly and boiled eggs.jpg",
        "Bò nướng đá ng29th8n2022 (nướng trên đá) (1).jpg", "Bò nướng đá ng29th8n2022 (nướng trên đá) (2).jpg", "Cánh gà nướng và chân gà nướng ở chợ BL ng7th10n2020 (1).jpg", "01 Food in Gran Canaria - fish and seafood mixed grill plate.jpg", "02 Grilled fish and seafood dinner in Canary Islands - Gran Canaria restaurant, marisco mixto a la parrilla.jpg", "Austern mit Sekt.jpg", "06-Kep Crab Market Cambodia-nX-12.jpg", "Bắp nướng kiểu Việt Nam ở Đà Lạt năm 2011.jpg", "Betel Beef Rolls Noodles Salad - Yen's Kitchen 2026-06-14 (1).jpg", "Cánh gà nướng và chân gà nướng ở đường NS ng27th4n2013 (1).jpg",
        "Banh mi assemblage.JPG", "20180409-22Nha Trang tour Bánh mì street store.jpg", "Banh Xeo with fish sauce and vegetables.jpg", "Bánh xèo (20-8-2020).jpg", "01 Baoguette Pork Banh Mi.jpg", "Bánh xèo 1.jpg", "Banh mi and cuon.jpg", "Bánh xèo 2.jpg", "Bánh bèo, Bánh ít trần, Bánh bột lọc trần.jpg", "Banh Khoai (4265580561).jpg",
        "Bahn Xeo (24061366245).jpg", "Bánh xèo (15826153307).jpg", "Banh Xeo Restaurant Hanoi.JPG", "Banh mi at Eden Center (4380293171).jpg", "02 Baoguette Pork Banh Mi.jpg", "Banh mi at Eden Center (4381046538).jpg", "Banh mi - vietnamese bread - (cut out from flickr5607479129).jpg", "Banh mi - flickr5607479129.jpg", "Bánh xèo 2014-07-10 10-39.jpg", "Banh xeo with veggies on a plate.jpg",
        "Chè bà ba.jpg", "Chè khúc bạch ở quán Thảo Vy 2020 09 06.jpg", "Chendol2.jpg", "Chè Thưng.jpg", "Chè Thạch Trắng Đậu Xanh Nhãn Nhục.JPG", "Banhchay.JPG", "A cup, spoon and pot of Vietnamese dessert- banana, tapioca pudding.jpg", "Banana, tapioca, coconut creme Vietnamese pudding (che chuoi).jpg", "Banana, tapioca, coconut creme Vietnamese pudding (che chuoi) - 28356997961.jpg", "Chè Bắp.jpg",
        "Cafe 29, Tết 2022 (cà phê Khe Sanh) (1).jpg", "Cafe 29, Tết 2022 (cà phê Khe Sanh) (2).jpg", "Cafe 29, Tết 2022 (cà phê Khe Sanh) (3).jpg", "Cafe 2K20 ng21th5n2022 (tách cà phê sữa) (1).jpg", "02024 1286 Orange juice.jpg", "A woman holding a glass of cane juice.jpg", "9750Foods Fruits Baliuag Bulacan Philippines 37.jpg", "5litre Sugarcane Juice.jpg", "Ca-Phe-Rang-Xay-Tai-Nha.jpg", "2023-12-10 Coffee and bánh mì in Saigon.jpg",
        "Mì Quảng chay, tháng 9 năm 2018 (1).jpg", "Mì Quảng chay, tháng 9 năm 2018 (2).jpg", "Mì Quảng chay, tháng 9 năm 2018 (3).jpg", "Mì Quảng chay, tháng 9 năm 2018 (4).jpg", "Mì Quảng chay, tháng 9 năm 2018 (5).jpg", "Mì Quảng chay, tháng 9 năm 2018 (6).jpg", "Tofu noodle soup (Phở chay) - Pho Hanoi Authentic 2024-12-01.jpg", "Cam Lộ th5n2023 (quán cơm chay cạnh chùa Cam Lộ) (1).jpg", "Bun tron, Ô Saigon, Galerie Vaugirard, Paris 001.jpg", "Bun tron, Ô Saigon, Galerie Vaugirard, Paris 004.jpg",
        "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (1).jpg", "06-Kep Crab Market Cambodia-nX-15.jpg", "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (2).jpg", "06-Kep Crab Market Cambodia-nX-7.jpg", "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (3).jpg", "Aburi Hotate, Aburi Salmon - Kura AUD9.80 (3722832977).jpg", "2005oyster.PNG", "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (4).jpg", "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (5).jpg", "Bữa ăn gia đình (món mực sim luộc), Tết năm 2019 (6).jpg"
    ];

    private static readonly FoodSeed[] Foods = BuildFoods();

    private sealed record BoothSeed(string Name, string Category, string Description, string SearchAliases);
    private sealed record FoodSeed(int Booth, string Name, string EnglishName, decimal Price, string FileName);
}
