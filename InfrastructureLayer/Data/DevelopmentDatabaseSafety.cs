using Microsoft.Extensions.Configuration;
using Npgsql;

namespace InfrastructureLayer.Data;

public static class DevelopmentDatabaseSafety
{
    public static string ResolveConnectionString(IConfiguration configuration, string environmentName)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing. Configure it outside source control.");
        }

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var localDatabaseName = configuration["Database:DevelopmentDatabaseName"]?.Trim();
        if (string.IsNullOrWhiteSpace(localDatabaseName)
            || (!localDatabaseName.Contains("local", StringComparison.OrdinalIgnoreCase)
                && !localDatabaseName.Contains("dev", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Development requires Database:DevelopmentDatabaseName containing 'Local' or 'Dev'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!IsLoopbackHost(builder.Host))
        {
            throw new InvalidOperationException(
                $"Development database host must be loopback. Refusing host '{builder.Host}'.");
        }

        builder.Database = localDatabaseName;
        return builder.ConnectionString;
    }

    public static string Describe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"Host={builder.Host};Port={builder.Port};Database={builder.Database}";
    }

    private static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.Trim().Trim('[', ']');
        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.Net.IPAddress.TryParse(normalized, out var address)
            && System.Net.IPAddress.IsLoopback(address);
    }
}
