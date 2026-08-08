using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureLayer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Data.Seeders;

public static class PackageImageSeeder
{
    private static readonly Dictionary<string, string> SeedImageUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        { "MARKET_BASIC", "/uploads/images/packages/seed/market_basic.png" },
        { "MARKET_PRO", "/uploads/images/packages/seed/market_pro.png" },
        { "BOOTH_FREE", "/uploads/images/packages/seed/booth_free.png" },
        { "BOOTH_GROWTH", "/uploads/images/packages/seed/booth_growth.png" },
        { "BOOTH_FEATURED", "/uploads/images/packages/seed/booth_featured.png" }
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SNMDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<SNMDbContext>>();

        EnsureSeedFilesInStorageRoot(scope.ServiceProvider, logger);

        var codes = SeedImageUrls.Keys.ToList();
        var packages = await context.Packages
            .Where(p => !p.IsDeleted && p.Code != null && codes.Contains(p.Code))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var updated = 0;
        foreach (var package in packages)
        {
            if (!string.IsNullOrWhiteSpace(package.ImageUrl))
                continue;

            package.ImageUrl = SeedImageUrls[package.Code!];
            package.UpdatedAt = now;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(ct);
            logger?.LogInformation("PackageImageSeeder assigned seed images to {Count} package(s).", updated);
        }
    }

    // Seed PNGs ship inside wwwroot, but /uploads/images is served from UploadStorage:RootPath
    // when configured — copy the files there so the seeded URLs resolve in every environment.
    private static void EnsureSeedFilesInStorageRoot(IServiceProvider services, ILogger? logger)
    {
        var env = services.GetService<IWebHostEnvironment>();
        var configuration = services.GetService<IConfiguration>();
        if (env is null || configuration is null)
            return;

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var configuredPath = configuration["UploadStorage:RootPath"];
        // LocalFileStorageService exposes /uploads from wwwroot/uploads when no
        // external storage root is configured. Keep seed files on that exact
        // contract as well; using wwwroot directly makes /uploads/images/... 404.
        var storageRoot = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(webRoot, "uploads")
            : Path.GetFullPath(configuredPath);

        foreach (var url in SeedImageUrls.Values)
        {
            var relativePath = url.Replace("/uploads/", string.Empty).Replace('/', Path.DirectorySeparatorChar);
            var source = Path.Combine(webRoot, relativePath);
            var target = Path.Combine(storageRoot, relativePath);
            try
            {
                if (!File.Exists(source))
                {
                    logger?.LogWarning("Package seed image source is missing: {Source}.", source);
                    continue;
                }

                if (File.Exists(target))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "PackageImageSeeder could not copy seed image to {Target}.", target);
            }
        }
    }
}
