using Finora.Application.Common;
using Finora.Domain.Common;
using Finora.Domain.Entities;
using Finora.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Finora.Infrastructure.Persistence;

public class FinoraDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserService? _currentUserService;

    public FinoraDbContext(
        DbContextOptions<FinoraDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Phase 2 — Core Finance
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionSplit> TransactionSplits => Set<TransactionSplit>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply custom configurations
        builder.ApplyConfigurationsFromAssembly(typeof(FinoraDbContext).Assembly);

        // Rename ASP.NET Identity tables to snake_case
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(u => u.Id).HasColumnName("id");
            entity.Property(u => u.Email).HasColumnName("email");
            entity.Property(u => u.NormalizedEmail).HasColumnName("normalized_email");
            entity.Property(u => u.UserName).HasColumnName("username");
            entity.Property(u => u.NormalizedUserName).HasColumnName("normalized_username");
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
            entity.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(u => u.PhoneNumber).HasColumnName("phone_number");
            entity.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            entity.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
            entity.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(128).IsRequired();
            entity.Property(u => u.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(u => u.RefreshTokenHash).HasColumnName("refresh_token_hash");
            entity.Property(u => u.RefreshTokenExpiryTime).HasColumnName("refresh_token_expiry_time");
            entity.Property(u => u.RefreshTokenRevoked).HasColumnName("refresh_token_revoked");
        });

        builder.Entity<IdentityRole<Guid>>(entity =>
        {
            entity.ToTable("roles");
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Name).HasColumnName("name");
            entity.Property(r => r.NormalizedName).HasColumnName("normalized_name");
            entity.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        });

        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("user_roles");
            entity.Property(ur => ur.UserId).HasColumnName("user_id");
            entity.Property(ur => ur.RoleId).HasColumnName("role_id");
        });

        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("user_claims");
            entity.Property(uc => uc.Id).HasColumnName("id");
            entity.Property(uc => uc.UserId).HasColumnName("user_id");
            entity.Property(uc => uc.ClaimType).HasColumnName("claim_type");
            entity.Property(uc => uc.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("user_logins");
            entity.Property(ul => ul.LoginProvider).HasColumnName("login_provider");
            entity.Property(ul => ul.ProviderKey).HasColumnName("provider_key");
            entity.Property(ul => ul.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(ul => ul.UserId).HasColumnName("user_id");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("role_claims");
            entity.Property(rc => rc.Id).HasColumnName("id");
            entity.Property(rc => rc.RoleId).HasColumnName("role_id");
            entity.Property(rc => rc.ClaimType).HasColumnName("claim_type");
            entity.Property(rc => rc.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.Property(ut => ut.UserId).HasColumnName("user_id");
            entity.Property(ut => ut.LoginProvider).HasColumnName("login_provider");
            entity.Property(ut => ut.Name).HasColumnName("name");
            entity.Property(ut => ut.Value).HasColumnName("value");
        });

        // Configure Global Query Filter for multi-tenancy
        builder.Entity<HouseholdMember>()
            .HasQueryFilter(m => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 m.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<Invitation>()
            .HasQueryFilter(i => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 i.HouseholdId == _currentUserService.HouseholdId);

        // Phase 2 tenant filters
        builder.Entity<Account>()
            .HasQueryFilter(a => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 a.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<Category>()
            .HasQueryFilter(c => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 c.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<Transaction>()
            .HasQueryFilter(t => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 t.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<TransactionSplit>()
            .HasQueryFilter(s => _currentUserService == null ||
                                 _currentUserService.HouseholdId == null ||
                                 s.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<Transfer>()
            .HasQueryFilter(tr => _currentUserService == null ||
                                  _currentUserService.HouseholdId == null ||
                                  tr.HouseholdId == _currentUserService.HouseholdId);

        builder.Entity<AccessGrant>()
            .HasQueryFilter(ag => _currentUserService == null ||
                                  _currentUserService.HouseholdId == null ||
                                  ag.HouseholdId == _currentUserService.HouseholdId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<IAuditable>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

