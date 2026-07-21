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
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "PresentationLayer");
            LoadDotEnv(Path.Combine(configPath, ".env"));

            var configuration = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing. Set ConnectionStrings__DefaultConnection in PresentationLayer/.env.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<SNMDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new SNMDbContext(optionsBuilder.Options);
        }

        private static void LoadDotEnv(string path)
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
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
