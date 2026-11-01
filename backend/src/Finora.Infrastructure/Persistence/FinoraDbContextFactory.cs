using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Finora.Infrastructure.Persistence;

public class FinoraDbContextFactory : IDesignTimeDbContextFactory<FinoraDbContext>
{
    public FinoraDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinoraDbContext>();
        
        // Dummy PostgreSQL connection string for migration generation
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                               ?? "Host=localhost;Port=5432;Database=finora;Username=postgres;Password=postgres;";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(FinoraDbContext).Assembly.FullName);
        });

        return new FinoraDbContext(optionsBuilder.Options);
    }
}

