using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InfrastructureLayer.Data
{
    public class SNMContextFactory : IDesignTimeDbContextFactory<SNMDbContext>
    {
        public SNMDbContext CreateDbContext(string[] args)
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(environmentName))
            {
                throw new InvalidOperationException(
                    "Set ASPNETCORE_ENVIRONMENT explicitly before running EF commands.");
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            var configPath = new[]
                {
                    Path.GetFullPath(Path.Combine(currentDirectory, "..", "PresentationLayer")),
                    Path.GetFullPath(Path.Combine(currentDirectory, "PresentationLayer")),
                    AppContext.BaseDirectory
                }
                .FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")))
                ?? throw new DirectoryNotFoundException(
                    "Could not locate appsettings.json for SNMDbContext design-time configuration.");
            if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                LoadDotEnv(Path.Combine(configPath, ".env.local"));
            }
            LoadDotEnv(Path.Combine(configPath, ".env"));

            var configuration = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = DevelopmentDatabaseSafety.ResolveConnectionString(
                configuration,
                environmentName);

            var optionsBuilder = new DbContextOptionsBuilder<SNMDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new SNMDbContext(optionsBuilder.Options);
        }

        public static void LoadDotEnv(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
