using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RentApp.Infrastructure.Configuration;
using RentApp.Infrastructure.Security;

namespace RentApp.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> tooling. Reads DATABASE_CONNECTION_STRING from the environment or the repo .env.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        DotEnvFile.LoadFromNearest(Directory.GetCurrentDirectory());
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DATABASE_CONNECTION_STRING is not set. Copy .env.example to .env at the repo root and fill it in.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureNpgsql(options, connectionString);
        return new AppDbContext(options.Options, AnonymousCurrentUser.Instance);
    }
}
