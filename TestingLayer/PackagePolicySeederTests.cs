using System.Text.Json;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace TestingLayer;

public class PackagePolicySeederTests
{
    private const string FirstSharedPaidTerm = "This subscription becomes active only after the payment is completed successfully.";
    private const string LastSharedPaidTerm = "A future policy version applies only to purchases, renewals or package changes that accept that newer version after its effective date.";

    private static readonly Dictionary<string, string> ExpectedTitles = new()
    {
        ["MARKET_BASIC"] = "Market Basic Subscription Policy",
        ["MARKET_PRO"] = "Market Pro Subscription Policy",
        ["BOOTH_FREE"] = "Booth Basic Usage Policy",
        ["BOOTH_GROWTH"] = "Booth Boost Subscription Policy",
        ["BOOTH_FEATURED"] = "Booth Featured Subscription Policy"
    };

    private static (ServiceProvider Provider, DbContextOptions<SNMDbContext> Options) CreateProvider(string dbName)
    {
        var options = new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var services = new ServiceCollection();
        services.AddDbContext<SNMDbContext>(o => o
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddLogging();
        return (services.BuildServiceProvider(), options);
    }

    private static async Task<Dictionary<string, Guid>> SeedPackagesAsync(DbContextOptions<SNMDbContext> options, params string[] excludeCodes)
    {
        var packages = new[]
        {
            new Package { Id = Guid.NewGuid(), Code = "MARKET_BASIC", PackageName = "Market Basic", Price = 100 },
            new Package { Id = Guid.NewGuid(), Code = "MARKET_PRO", PackageName = "Market Pro", Price = 300 },
            new Package { Id = Guid.NewGuid(), Code = "BOOTH_FREE", PackageName = "Booth Basic", Price = 0 },
            new Package { Id = Guid.NewGuid(), Code = "BOOTH_GROWTH", PackageName = "Booth Boost", Price = 50 },
            new Package { Id = Guid.NewGuid(), Code = "BOOTH_FEATURED", PackageName = "Booth Featured", Price = 150 }
        }.Where(p => !excludeCodes.Contains(p.Code)).ToList();

        using var seedCtx = new SNMDbContext(options);
        seedCtx.Packages.AddRange(packages);
        await seedCtx.SaveChangesAsync();
        return packages.ToDictionary(p => p.Code!, p => p.Id);
    }

    private static async Task AddPolicyAsync(DbContextOptions<SNMDbContext> options, Guid packageId, string version, bool isActive, string contentJson = "{\"terms\":[\"existing\"]}", string title = "Existing Policy")
    {
        using var ctx = new SNMDbContext(options);
        ctx.PackagePolicies.Add(new PackagePolicy
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            Version = version,
            Title = title,
            ContentJson = contentJson,
            ContentMarkdown = null,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = isActive,
            IsDeleted = false
        });
        await ctx.SaveChangesAsync();
    }

    private static List<PackagePolicy> GetPolicies(DbContextOptions<SNMDbContext> options, Guid packageId)
    {
        using var ctx = new SNMDbContext(options);
        return ctx.PackagePolicies.Where(p => p.PackageId == packageId).ToList();
    }

    private static List<string> GetTerms(PackagePolicy policy)
    {
        using var doc = JsonDocument.Parse(policy.ContentJson);
        return doc.RootElement.GetProperty("terms").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesFiveActiveVersionOnePolicies()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.Equal(5, report.Count);
        Assert.All(report.Values, msg => Assert.StartsWith("CREATE policy 1.0", msg));

        using var verifyCtx = new SNMDbContext(options);
        var policies = verifyCtx.PackagePolicies.ToList();
        Assert.Equal(5, policies.Count);
        Assert.All(policies, p => Assert.Equal("1.0", p.Version));
        Assert.All(policies, p => Assert.True(p.IsActive));
        Assert.All(policies, p => Assert.False(p.IsDeleted));
        foreach (var (code, title) in ExpectedTitles)
        {
            var policy = policies.Single(p => p.PackageId == packageIds[code]);
            Assert.Equal(title, policy.Title);
        }
    }

    [Fact]
    public async Task SeedAsync_BoothFree_HasTwentyTermsWithoutPaidSubscriptionTerms()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);

        await PackagePolicySeeder.SeedAsync(provider);

        var policy = GetPolicies(options, packageIds["BOOTH_FREE"]).Single();
        var terms = GetTerms(policy);

        Assert.Equal(20, terms.Count);
        Assert.Equal("Booth Basic is assigned automatically when an eligible booth is created and assigned by a Market Owner.", terms[0]);
        Assert.Equal("Future changes to the Booth Basic policy apply after their effective date and do not rewrite previously stored subscription or activity history.", terms[^1]);
        Assert.DoesNotContain(FirstSharedPaidTerm, terms);
        Assert.DoesNotContain(LastSharedPaidTerm, terms);
        Assert.All(terms, t => Assert.DoesNotContain("subscription becomes active", t));
        Assert.All(terms, t => Assert.DoesNotContain("payment is completed", t));
        Assert.All(terms, t => Assert.DoesNotContain("prorated", t));
        Assert.Contains("## Policy Terms", policy.ContentMarkdown);
        Assert.DoesNotContain("## Package Specific Policies", policy.ContentMarkdown);
        Assert.DoesNotContain("## General Subscription Terms", policy.ContentMarkdown);
    }

    [Theory]
    [InlineData("MARKET_BASIC", 12, "Market Basic allows the Market Owner to manage one night market.")]
    [InlineData("MARKET_PRO", 15, "Market Pro includes all Market Basic features.")]
    [InlineData("BOOTH_GROWTH", 14, "Booth Boost includes all Booth Basic features.")]
    [InlineData("BOOTH_FEATURED", 16, "Booth Featured includes all Booth Boost features.")]
    public async Task SeedAsync_PaidPackage_AppendsSharedTermsAfterSpecificTerms(string code, int specificCount, string firstSpecificTerm)
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);

        await PackagePolicySeeder.SeedAsync(provider);

        var policy = GetPolicies(options, packageIds[code]).Single();
        var terms = GetTerms(policy);

        Assert.Equal(specificCount + 20, terms.Count);
        Assert.Equal(firstSpecificTerm, terms[0]);
        Assert.Equal(FirstSharedPaidTerm, terms[specificCount]);
        Assert.Equal(LastSharedPaidTerm, terms[^1]);
        Assert.Contains("## Package Specific Policies", policy.ContentMarkdown);
        Assert.Contains("## General Subscription Terms", policy.ContentMarkdown);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_SecondRunIsNoOpWithSkipReport()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        await SeedPackagesAsync(options);

        await PackagePolicySeeder.SeedAsync(provider);

        Dictionary<Guid, string> firstRunContent;
        using (var midCtx = new SNMDbContext(options))
        {
            firstRunContent = midCtx.PackagePolicies.ToDictionary(p => p.Id, p => p.ContentJson);
        }

        var secondReport = await PackagePolicySeeder.SeedAsync(provider);

        Assert.Equal(5, secondReport.Count);
        Assert.All(secondReport.Values, msg => Assert.StartsWith("SKIP", msg));
        Assert.All(secondReport.Values, msg => Assert.Contains("already has an active policy", msg));

        using var verifyCtx = new SNMDbContext(options);
        var policies = verifyCtx.PackagePolicies.ToList();
        Assert.Equal(5, policies.Count);
        Assert.All(policies, p => Assert.Equal(firstRunContent[p.Id], p.ContentJson));
    }

    [Fact]
    public async Task SeedAsync_ExistingActivePolicy_IsSkippedAndContentUntouched()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);
        var existingJson = "{\"terms\":[\"admin custom term\"]}";
        await AddPolicyAsync(options, packageIds["MARKET_BASIC"], "0.9", isActive: true, contentJson: existingJson, title: "Admin Policy");

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.StartsWith("SKIP", report["MARKET_BASIC"]);
        Assert.Contains("already has an active policy", report["MARKET_BASIC"]);

        var policies = GetPolicies(options, packageIds["MARKET_BASIC"]);
        var existing = Assert.Single(policies);
        Assert.Equal("0.9", existing.Version);
        Assert.Equal("Admin Policy", existing.Title);
        Assert.Equal(existingJson, existing.ContentJson);

        using var verifyCtx = new SNMDbContext(options);
        Assert.Equal(5, verifyCtx.PackagePolicies.Count());
    }

    [Fact]
    public async Task SeedAsync_TwoActivePolicies_ReportsDataErrorAndMakesNoChanges()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);
        await AddPolicyAsync(options, packageIds["MARKET_PRO"], "0.8", isActive: true);
        await AddPolicyAsync(options, packageIds["MARKET_PRO"], "0.9", isActive: true);

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.StartsWith("DATA ERROR", report["MARKET_PRO"]);

        var policies = GetPolicies(options, packageIds["MARKET_PRO"]);
        Assert.Equal(2, policies.Count);
        Assert.Equal(new[] { "0.8", "0.9" }, policies.Select(p => p.Version).OrderBy(v => v).ToArray());
        Assert.All(policies, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task SeedAsync_ExistingDraft_ReportsNeedsActivationAndDoesNotActivate()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);
        await AddPolicyAsync(options, packageIds["BOOTH_GROWTH"], "0.9", isActive: false);

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.StartsWith("NEEDS_ACTIVATION", report["BOOTH_GROWTH"]);

        var policies = GetPolicies(options, packageIds["BOOTH_GROWTH"]);
        var draft = Assert.Single(policies);
        Assert.False(draft.IsActive);
        Assert.Equal("0.9", draft.Version);
    }

    [Fact]
    public async Task SeedAsync_ExistingInactiveVersionOne_BlocksCreationWithDuplicateReport()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        var packageIds = await SeedPackagesAsync(options);
        await AddPolicyAsync(options, packageIds["BOOTH_FEATURED"], "1.0", isActive: false);

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.StartsWith("SKIP", report["BOOTH_FEATURED"]);
        Assert.Contains("already has a policy with version '1.0'", report["BOOTH_FEATURED"]);

        var policies = GetPolicies(options, packageIds["BOOTH_FEATURED"]);
        var existing = Assert.Single(policies);
        Assert.False(existing.IsActive);
        Assert.Equal("Existing Policy", existing.Title);
    }

    [Fact]
    public async Task SeedAsync_ContentMarkdown_ContainsEveryTermFromContentJson()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        await SeedPackagesAsync(options);

        await PackagePolicySeeder.SeedAsync(provider);

        using var verifyCtx = new SNMDbContext(options);
        var policies = verifyCtx.PackagePolicies.ToList();
        Assert.Equal(5, policies.Count);
        foreach (var policy in policies)
        {
            Assert.NotNull(policy.ContentMarkdown);
            Assert.StartsWith($"# {policy.Title}", policy.ContentMarkdown);
            foreach (var term in GetTerms(policy))
            {
                Assert.Contains($"- {term}", policy.ContentMarkdown);
            }
        }
    }

    [Fact]
    public async Task SeedAsync_DryRun_CreatesNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        await SeedPackagesAsync(options);

        var report = await PackagePolicySeeder.SeedAsync(provider, dryRun: true);

        Assert.Equal(5, report.Count);
        Assert.All(report.Values, msg => Assert.Contains("[DRY RUN]", msg));

        using var verifyCtx = new SNMDbContext(options);
        Assert.Empty(verifyCtx.PackagePolicies.ToList());
    }

    [Fact]
    public async Task SeedAsync_MissingPackage_ReportsErrorAndSeedsOthers()
    {
        var dbName = Guid.NewGuid().ToString();
        var (provider, options) = CreateProvider(dbName);
        await SeedPackagesAsync(options, "MARKET_PRO");

        var report = await PackagePolicySeeder.SeedAsync(provider);

        Assert.StartsWith("ERROR", report["MARKET_PRO"]);
        Assert.Contains("not found", report["MARKET_PRO"]);

        using var verifyCtx = new SNMDbContext(options);
        Assert.Equal(4, verifyCtx.PackagePolicies.Count());
    }
}
