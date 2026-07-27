using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Data.Seeders;

public static class PackagePolicySeeder
{
    private static readonly List<string> SharedPaidSubscriptionTerms = new()
    {
        "This subscription becomes active only after the payment is completed successfully.",
        "A pending, cancelled, failed or expired payment does not activate the subscription or unlock package features.",
        "Package benefits are available only during the recorded active subscription period.",
        "The subscription duration starts from its recorded activation date, except for renewals and scheduled downgrades.",
        "Renewing the same package extends access from the end date of the current active subscription. It does not replace the remaining active period.",
        "An upgrade to a higher package becomes active immediately after the upgrade payment is completed successfully.",
        "When upgrading, the unused value of the current paid subscription may be applied as prorated credit toward the upgrade price.",
        "Prorated upgrade credit is not a cash refund and cannot be withdrawn, transferred or used for an unrelated purchase.",
        "If prorated credit covers the full upgrade amount, the upgrade may be activated without an additional payment.",
        "A downgrade to a lower package is scheduled to begin after the current paid subscription ends. It does not remove the current package immediately.",
        "No cash refund is provided for unused subscription time when the account, night market or booth is voluntarily deleted, deactivated or no longer used.",
        "Only one pending payment or scheduled package change may exist at a time for the same subscription owner.",
        "Package benefits cannot be transferred to another account, night market owner or booth owner.",
        "If a package is no longer available for sale, an existing active subscription remains valid until its recorded end date.",
        "Existing business data and subscription history are retained after expiration, upgrade or downgrade.",
        "Resources exceeding the new package limits are not automatically deleted, but related operations may be restricted until usage returns within the active package limits.",
        "Suspended accounts, night markets or booths cannot use package benefits while the suspension remains active.",
        "Suspension or enforcement caused by a policy violation does not automatically create a refund.",
        "The policy version accepted during purchase or renewal is stored with the subscription and remains part of that transaction's history.",
        "A future policy version applies only to purchases, renewals or package changes that accept that newer version after its effective date."
    };

    private class PolicyDefinition
    {
        public string Code { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Version { get; set; } = "1.0";
        public List<string> SpecificTerms { get; set; } = new();
        public bool IncludePaidSubscriptionTerms { get; set; }
    }

    private static readonly List<PolicyDefinition> Definitions = new()
    {
        new PolicyDefinition
        {
            Code = "MARKET_BASIC",
            Title = "Market Basic Subscription Policy",
            IncludePaidSubscriptionTerms = true,
            SpecificTerms = new List<string>
            {
                "Market Basic allows the Market Owner to manage one night market.",
                "The night market may contain up to 30 booth slots.",
                "The night market may contain one layout configuration.",
                "The Market Owner may manage market information, address, operating hours, description and market images.",
                "The Market Owner may create booth slots and assign eligible booths to available slots.",
                "Market Basic uses one general market area. Food, Drink and Dessert zone management is not included.",
                "The Market Owner may view basic booth counts, order statistics and market activity information.",
                "Basic complaint management is included. Priority handling, SLA tracking and advanced complaint analytics are not included.",
                "Creating additional night markets, layouts or booth slots beyond the package limits is blocked.",
                "If the subscription expires, paid Market features are locked until the subscription is renewed or another eligible Market package becomes active.",
                "Existing markets, layouts, booths, orders and complaint history are not automatically deleted after expiration.",
                "If the Market Owner previously used Market Pro, resources exceeding Market Basic limits may remain stored but may be restricted from editing, activation or further expansion."
            }
        },
        new PolicyDefinition
        {
            Code = "MARKET_PRO",
            Title = "Market Pro Subscription Policy",
            IncludePaidSubscriptionTerms = true,
            SpecificTerms = new List<string>
            {
                "Market Pro includes all Market Basic features.",
                "Market Pro allows the Market Owner to manage up to three night markets.",
                "Each night market may contain up to 150 booth slots.",
                "Each night market may contain up to five layout configurations.",
                "Only one layout may be active for a night market at a time.",
                "Market Pro includes Food, Drink and Dessert zone management.",
                "Zone capacity and package slot limits remain enforced when generating or editing a layout.",
                "The Market Owner may use advanced booth management and booth risk indicators.",
                "Priority complaint handling and SLA tracking are included while the subscription is active.",
                "Advanced complaint dashboards, market activity reports, booth performance reports and sales activity insights are included.",
                "Report export is available only while the Market Pro subscription is active.",
                "Exported reports reflect the data available at the time of export and do not guarantee future business performance.",
                "Recommendation, risk and performance indicators are decision-support information and do not guarantee customer visits, sales or complaint outcomes.",
                "When upgrading from Market Basic, eligible unused paid value from Market Basic may be applied as prorated credit.",
                "When downgrading to Market Basic, the change begins after Market Pro expires. Excess markets, layouts, zones or slots are not automatically deleted but may become restricted."
            }
        },
        new PolicyDefinition
        {
            Code = "BOOTH_FREE",
            Title = "Booth Basic Usage Policy",
            IncludePaidSubscriptionTerms = false,
            SpecificTerms = new List<string>
            {
                "Booth Basic is assigned automatically when an eligible booth is created and assigned by a Market Owner.",
                "No purchase or payment is required to activate Booth Basic.",
                "Booth Basic has no fixed subscription expiration while the booth and Booth Owner remain eligible to use the platform.",
                "A Booth Owner cannot independently create or assign a booth to a night market. Booth creation and slot assignment are managed by the Market Owner.",
                "Booth Basic supports one booth per Booth Owner.",
                "The Booth Owner may edit the assigned booth's name, description, images and operating hours.",
                "The Booth Owner may manage up to 20 menu items, including categories, prices, descriptions, images and availability.",
                "The Booth Owner may receive orders, confirm orders and update eligible order statuses.",
                "The Booth Owner may create orders for customers according to the platform's order rules.",
                "The Booth Owner may chat with customers through supported platform features.",
                "The Booth Owner may view customer reviews but cannot reply to reviews under Booth Basic.",
                "The Booth Owner may view basic order, revenue and rating statistics.",
                "Promotions, combos, advanced analytics, Featured badges and paid recommendation priority are not included.",
                "When the menu reaches 20 items, new menu items cannot be created until an existing item is removed or a higher package becomes active.",
                "Removing or releasing the booth ends access to booth-specific operations but does not automatically delete historical orders, reviews, messages or subscription history.",
                "A suspended Booth Owner or booth cannot use Booth Basic benefits while the suspension remains active.",
                "Booth Basic benefits cannot be transferred to another Booth Owner account.",
                "When a paid Booth package expires without a scheduled paid replacement, the booth returns to Booth Basic entitlements.",
                "Returning to Booth Basic does not delete excess menu items, but items exceeding the Booth Basic limit may be locked from further activation or editing until usage complies with the limit.",
                "Future changes to the Booth Basic policy apply after their effective date and do not rewrite previously stored subscription or activity history."
            }
        },
        new PolicyDefinition
        {
            Code = "BOOTH_GROWTH",
            Title = "Booth Boost Subscription Policy",
            IncludePaidSubscriptionTerms = true,
            SpecificTerms = new List<string>
            {
                "Booth Boost includes all Booth Basic features.",
                "Unlimited menu item management is available while the Booth Boost subscription is active.",
                "The Booth Owner may reply to customer reviews while the subscription is active.",
                "The Booth Owner may create and manage eligible promotions and combos.",
                "Best-selling item analytics and peak-hour analytics are included.",
                "Booth Boost provides Tier 2 recommendation priority for relevant customer searches and recommendations.",
                "Recommendation priority improves eligibility and ranking signals but does not guarantee placement, impressions, visits, orders or revenue.",
                "Promotions and combos must comply with price, date, food availability and platform validation rules.",
                "An expired, inactive or suspended Booth Boost subscription removes access to paid Boost features.",
                "If no other paid Booth subscription becomes active, expiration returns the booth to Booth Basic entitlements.",
                "Existing menu items, promotions, review replies, analytics history and orders are not automatically deleted after expiration.",
                "Promotions requiring Booth Boost cannot remain newly active after the subscription expires.",
                "When upgrading from Booth Basic, no prorated credit is provided because Booth Basic is free.",
                "When changing from Booth Featured to Booth Boost, the downgrade starts after the active Booth Featured subscription ends."
            }
        },
        new PolicyDefinition
        {
            Code = "BOOTH_FEATURED",
            Title = "Booth Featured Subscription Policy",
            IncludePaidSubscriptionTerms = true,
            SpecificTerms = new List<string>
            {
                "Booth Featured includes all Booth Boost features.",
                "The booth may display a Featured badge while the Booth Featured subscription is active.",
                "Booth Featured provides the highest recommendation priority available among current Booth packages.",
                "Higher recommendation priority improves ranking eligibility for relevant customer searches and recommendations but does not guarantee a specific position.",
                "Featured status does not guarantee customer impressions, booth visits, orders, revenue or business results.",
                "The Booth Owner may select eligible food items to be highlighted as Featured food.",
                "Featured food items must remain active, available and compliant with platform rules.",
                "Unavailable, deleted, hidden or non-compliant food items cannot continue to receive Featured treatment.",
                "Featured placement is not a paid advertisement and must not be described to users as guaranteed advertising.",
                "The platform may consider relevance, customer preferences, food availability, market status, booth status, quality signals and policy compliance when generating recommendations.",
                "Recommendation priority may be temporarily unavailable when the booth, night market or related food item is inactive or suspended.",
                "When upgrading from Booth Boost, eligible unused paid value may be applied as prorated credit toward Booth Featured.",
                "The previous Booth Boost subscription ends when the Booth Featured upgrade becomes active.",
                "If Booth Featured expires without another scheduled paid package, the booth returns to Booth Basic entitlements.",
                "The Featured badge, Featured food treatment and highest recommendation priority end when Booth Featured is no longer active.",
                "Existing booth data, food data, reviews, orders, promotions and analytics history are retained after expiration."
            }
        }
    };

    public static async Task<Dictionary<string, string>> SeedAsync(IServiceProvider serviceProvider, bool dryRun = false, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<SNMDbContext>>();

        var report = new Dictionary<string, string>();
        var now = DateTime.UtcNow;

        if (!dryRun)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            try
            {
                await ProcessSeedingAsync(context, logger, report, now, dryRun, ct);
                await context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                logger?.LogError(ex, "PackagePolicySeeder failed. Transaction rolled back.");
                throw;
            }
        }
        else
        {
            await ProcessSeedingAsync(context, logger, report, now, dryRun, ct);
        }

        return report;
    }

    private static async Task ProcessSeedingAsync(SNMDbContext context, ILogger? logger, Dictionary<string, string> report, DateTime now, bool dryRun, CancellationToken ct)
    {
        var packages = await context.Packages
            .Include(p => p.Policies)
            .Where(p => !p.IsDeleted)
            .ToListAsync(ct);

        foreach (var def in Definitions)
        {
            try
            {
                var package = packages.FirstOrDefault(p => string.Equals(p.Code, def.Code, StringComparison.OrdinalIgnoreCase));
                if (package == null)
                {
                    var msg = $"ERROR: Package with code '{def.Code}' not found in database.";
                    report[def.Code] = msg;
                    logger?.LogWarning(msg);
                    continue;
                }

                var existingPolicies = package.Policies.Where(p => !p.IsDeleted).ToList();

                var activeCount = existingPolicies.Count(p => p.IsActive && p.EffectiveFrom <= now);
                if (activeCount > 1)
                {
                    var msg = $"DATA ERROR: Package '{def.Code}' has {activeCount} active policies. No changes made.";
                    report[def.Code] = msg;
                    logger?.LogWarning(msg);
                    continue;
                }

                if (activeCount == 1)
                {
                    var msg = $"SKIP: Package '{def.Code}' already has an active policy.";
                    report[def.Code] = msg;
                    logger?.LogInformation(msg);
                    continue;
                }

                if (existingPolicies.Any(p => string.Equals(p.Version, def.Version, StringComparison.OrdinalIgnoreCase)))
                {
                    var msg = $"SKIP: Package '{def.Code}' already has a policy with version '{def.Version}'.";
                    report[def.Code] = msg;
                    logger?.LogInformation(msg);
                    continue;
                }

                if (existingPolicies.Any(p => !p.IsActive || p.EffectiveFrom > now))
                {
                    var msg = $"NEEDS_ACTIVATION: Package '{def.Code}' has an inactive or future policy that was not activated.";
                    report[def.Code] = msg;
                    logger?.LogInformation(msg);
                    continue;
                }

                var allTerms = new List<string>(def.SpecificTerms);
                if (def.IncludePaidSubscriptionTerms)
                {
                    allTerms.AddRange(SharedPaidSubscriptionTerms);
                }

                var contentJson = JsonSerializer.Serialize(new { terms = allTerms });
                var contentMarkdown = BuildMarkdown(def);

                var newId = Guid.NewGuid();
                var policyAction = $"CREATE policy {def.Version} (Id: {newId})";
                if (dryRun)
                {
                    report[def.Code] = $"[DRY RUN] {policyAction}";
                    logger?.LogInformation("[DRY RUN] Would create policy {Version} for package {Code}", def.Version, def.Code);
                }
                else
                {
                    var policy = new PackagePolicy
                    {
                        Id = newId,
                        PackageId = package.Id,
                        Version = def.Version,
                        Title = def.Title,
                        ContentJson = contentJson,
                        ContentMarkdown = contentMarkdown,
                        EffectiveFrom = now,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await context.PackagePolicies.AddAsync(policy, ct);
                    report[def.Code] = policyAction;
                    logger?.LogInformation("Created policy {Version} for package {Code}", def.Version, def.Code);
                }
            }
            catch (Exception ex)
            {
                report[def.Code] = $"ERROR: {ex.Message}";
                logger?.LogError(ex, "Failed to seed policy for package {Code}", def.Code);
            }
        }
    }

    private static string BuildMarkdown(PolicyDefinition def)
    {
        var lines = new List<string> { $"# {def.Title}", "" };
        if (def.IncludePaidSubscriptionTerms)
        {
            lines.Add("## Package Specific Policies");
            lines.Add("");
            lines.AddRange(def.SpecificTerms.Select(t => $"- {t}"));
            lines.Add("");
            lines.Add("## General Subscription Terms");
            lines.Add("");
            lines.AddRange(SharedPaidSubscriptionTerms.Select(t => $"- {t}"));
        }
        else
        {
            lines.Add("## Policy Terms");
            lines.Add("");
            lines.AddRange(def.SpecificTerms.Select(t => $"- {t}"));
        }
        return string.Join("\n", lines);
    }
}
