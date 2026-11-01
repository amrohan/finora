using Microsoft.AspNetCore.Identity;

namespace Finora.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? RefreshTokenExpiryTime { get; set; }
    public bool RefreshTokenRevoked { get; set; }
}

