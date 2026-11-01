using System.Security.Cryptography;
using Finora.Application.Common;
using Finora.Application.Features.Households;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/households")]
[Authorize]
public class HouseholdsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IJwtTokenService _tokenService;
    private readonly IAuditService _auditService;

    public HouseholdsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IJwtTokenService tokenService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> ListUserHouseholds()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var memberships = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .Include(m => m.Household)
            .Where(m => m.UserId == userId.Value)
            .ToListAsync();

        var result = memberships.Select(m => new HouseholdDto(
            m.Household!.Id,
            m.Household.Name,
            m.Household.Currency,
            m.Household.Timezone,
            m.Role,
            _dbContext.HouseholdMembers.IgnoreQueryFilters().Count(x => x.HouseholdId == m.HouseholdId),
            m.Household.CreatedAt
        )).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHousehold([FromBody] CreateHouseholdRequest request)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Currency = request.Currency,
            Timezone = request.Timezone,
            CreatedByUserId = userId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            UserId = userId.Value,
            Role = HouseholdRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Households.Add(household);
        _dbContext.HouseholdMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        // Seed default INR categories for this household
        await SystemCategorySeeder.SeedAsync(_dbContext, household.Id);

        await _auditService.LogAsync(
            household.Id,
            userId.Value,
            "HouseholdCreated",
            "Household",
            household.Id.ToString(),
            newValues: new { household.Name, household.Currency, household.Timezone });

        return CreatedAtAction(nameof(GetHousehold), new { id = household.Id }, new HouseholdDto(
            household.Id,
            household.Name,
            household.Currency,
            household.Timezone,
            HouseholdRole.Owner,
            1,
            household.CreatedAt));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetHousehold(Guid id)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        // Enforce tenancy check: User must be a member of this household
        var membership = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .Include(m => m.Household)
            .FirstOrDefaultAsync(m => m.HouseholdId == id && m.UserId == userId.Value);

        if (membership == null || membership.Household == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Household Not Found",
                Detail = "The specified household does not exist or you do not have permission to view it."
            });
        }

        var memberCount = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .CountAsync(m => m.HouseholdId == id);

        return Ok(new HouseholdDto(
            membership.Household.Id,
            membership.Household.Name,
            membership.Household.Currency,
            membership.Household.Timezone,
            membership.Role,
            memberCount,
            membership.Household.CreatedAt));
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListHouseholdMembers(Guid id)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        var callerMembership = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.HouseholdId == id && m.UserId == userId.Value);

        if (callerMembership == null)
        {
            return Forbid();
        }

        var members = await (
            from m in _dbContext.HouseholdMembers.IgnoreQueryFilters().Where(m => m.HouseholdId == id)
            join u in _dbContext.Users on m.UserId equals u.Id
            select new HouseholdMemberDto(
                u.Id,
                u.FullName,
                u.Email!,
                m.Role,
                m.JoinedAt)
        ).ToListAsync();

        return Ok(members);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> InviteMember(Guid id, [FromBody] InviteMemberRequest request)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized();

        // Check that caller is Admin or Owner of this household
        var callerMembership = await _dbContext.HouseholdMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.HouseholdId == id && m.UserId == userId.Value);

        if (callerMembership == null || (callerMembership.Role != HouseholdRole.Owner && callerMembership.Role != HouseholdRole.Admin))
        {
            return Forbid();
        }

        // Generate raw invitation token
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = _tokenService.HashToken(rawToken);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = id,
            Email = request.Email,
            Role = request.Role,
            TokenHash = tokenHash,
            InvitedByUserId = userId.Value,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            id,
            userId.Value,
            "MemberInvited",
            "Invitation",
            invitation.Id.ToString(),
            newValues: new { request.Email, request.Role });

        return Ok(new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            rawToken));
    }

    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> InspectInvitation(string token)
    {
        var tokenHash = _tokenService.HashToken(token);
        var invitation = await _dbContext.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Household)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending);

        if (invitation == null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Invitation",
                Detail = "The invitation is invalid or has expired."
            });
        }

        return Ok(new
        {
            invitation.Id,
            invitation.Email,
            invitation.Role,
            HouseholdName = invitation.Household?.Name ?? "Family Household",
            invitation.ExpiresAt
        });
    }
}

