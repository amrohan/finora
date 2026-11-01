using System.Text;
using Finora.Application.Common;
using Finora.Application.Features.Transactions;
using Finora.Infrastructure.Identity;
using Finora.Infrastructure.Persistence;
using Finora.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Finora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database Context
        var connectionString = configuration["DATABASE_CONNECTION_STRING"] 
                               ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<FinoraDbContext>((sp, options) =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString) && 
                (connectionString.Contains("Host=") || connectionString.Contains("Server=")))
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(FinoraDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(3);
                });
            }
            else
            {
                // In-memory fallback for lightweight testing/bootstrapping without database container
                options.UseInMemoryDatabase("FinoraDb");
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            }
        });

        // 2. ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<FinoraDbContext>()
        .AddDefaultTokenProviders();

        // 3. JWT Authentication
        var secret = configuration["JWT_SECRET"] ?? "default_finora_development_secret_key_at_least_32_chars_long";
        var issuer = configuration["JWT_ISSUER"] ?? "https://api.finora.internal";
        var audience = configuration["JWT_AUDIENCE"] ?? "https://finora.internal";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        // 4. Services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITransactionImportService, TransactionImportService>();

        return services;
    }
}

