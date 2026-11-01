using Finora.Application.Common;
using Finora.Application.Features.Identity;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Identity;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FinoraDbContext _dbContext;
    private readonly IJwtTokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        FinoraDbContext dbContext,
        IJwtTokenService tokenService,
        IAuditService auditService,
        IConfiguration configuration,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _auditService = auditService;
        _configuration = configuration;
        _currentUserService = currentUserService;
    }

    [HttpPost("register-initial")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterInitialAdmin([FromBody] RegisterInitialAdminRequest request)
    {
        var expectedToken = _configuration["INITIAL_SETUP_TOKEN"];
        if (string.IsNullOrWhiteSpace(expectedToken) || request.SetupToken != expectedToken)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Invalid or expired setup token."
            });
        }

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "A user with this email already exists."
            });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User Creation Failed",
                Detail = string.Join("; ", createResult.Errors.Select(e => e.Description))
            });
        }

        // Create initial household
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = request.HouseholdName,
            CreatedByUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = user.Id,
            Role = HouseholdRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Households.Add(household);
        _dbContext.HouseholdMembers.Add(member);

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenHash = _tokenService.HashToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        await _dbContext.SaveChangesAsync();

        // Seed default INR categories for this initial household
        await SystemCategorySeeder.SeedAsync(_dbContext, household.Id);

        await _auditService.LogAsync(
            household.Id,
            user.Id,
            "InitialAdminRegistered",
            "User",
            user.Id.ToString(),
            newValues: new { user.Email, user.FullName, household.Name });

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, household.Id, HouseholdRole.Owner);

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            3600,
            user.Id,
            user.Email!,
            user.FullName,
            household.Id,
            HouseholdRole.Owner));
    }

    [HttpPost("register-with-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterWithInvite([FromBody] RegisterWithInviteRequest request)
    {
        var tokenHash = _tokenService.HashToken(request.InviteToken);
        var invitation = await _dbContext.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending);

        if (invitation == null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Invitation",
                Detail = "The invitation link is invalid, expired, or has already been used."
            });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = invitation.Email,
            Email = invitation.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User Creation Failed",
                Detail = string.Join("; ", createResult.Errors.Select(e => e.Description))
            });
        }

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = invitation.HouseholdId,
            UserId = user.Id,
            Role = invitation.Role,
            JoinedAt = DateTimeOffset.UtcNow
        };

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTimeOffset.UtcNow;

        _dbContext.HouseholdMembers.Add(member);

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenHash = _tokenService.HashToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            invitation.HouseholdId,
            user.Id,
            "UserJoinedViaInvitation",
            "User",
            user.Id.ToString(),
            newValues: new { user.Email, member.Role });

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, invitation.HouseholdId, member.Role);

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            3600,
            user.Id,
            user.Email!,
            user.FullName,
            invitation.HouseholdId,
            member.Role));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication Failed",
                Detail = "Invalid email or password."
            });
        }

        // Determine active household
        HouseholdMember? membership = null;
        if (request.HouseholdId.HasValue)
        {
            membership = await _dbContext.HouseholdMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.UserId == user.Id && m.HouseholdId == request.HouseholdId.Value);
        }
        else
        {
            membership = await _dbContext.HouseholdMembers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.UserId == user.Id);
        }

        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenHash = _tokenService.HashToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        user.RefreshTokenRevoked = false;
        await _userManager.UpdateAsync(user);

        var activeHouseholdId = membership?.HouseholdId;
        var role = membership?.Role;

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, activeHouseholdId, role);

        await _auditService.LogAsync(
            activeHouseholdId,
            user.Id,
            "UserLogin",
            "User",
            user.Id.ToString());

        return Ok(new AuthResponse(
            accessToken,
            refreshToken,
            3600,
            user.Id,
            user.Email!,
            user.FullName,
            activeHouseholdId,
            role));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshTokenHash == tokenHash && 
                                      !u.RefreshTokenRevoked && 
                                      u.RefreshTokenExpiryTime > DateTimeOffset.UtcNow);

        if (user == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Token",
                Detail = "Refresh token is invalid or expired."
            });
        }

        var membership = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id && 
                                     (request.HouseholdId == null || m.HouseholdId == request.HouseholdId.Value));

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenHash = _tokenService.HashToken(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTimeOffset.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.Email!, membership?.HouseholdId, membership?.Role);

        return Ok(new AuthResponse(
            accessToken,
            newRefreshToken,
            3600,
            user.Id,
            user.Email!,
            user.FullName,
            membership?.HouseholdId,
            membership?.Role));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            ActiveHouseholdId = _currentUserService.HouseholdId,
            Role = _currentUserService.Role
        });
    }
}

