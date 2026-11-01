using System.Security.Claims;
using Finora.Application.Common;
using Finora.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Finora.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _explicitHouseholdId;
    private HouseholdRole? _explicitRole;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? _httpContextAccessor.HttpContext?.User.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public string? Email => 
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;

    public Guid? HouseholdId => _explicitHouseholdId ?? GetHouseholdFromClaims();

    public HouseholdRole? Role => _explicitRole ?? GetRoleFromClaims();

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public void SetTenant(Guid householdId, HouseholdRole role)
    {
        _explicitHouseholdId = householdId;
        _explicitRole = role;
    }

    private Guid? GetHouseholdFromClaims()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst("household_id");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    private HouseholdRole? GetRoleFromClaims()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role);
        return claim != null && Enum.TryParse<HouseholdRole>(claim.Value, true, out var role) ? role : null;
    }
}

