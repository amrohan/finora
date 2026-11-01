using Finora.Application.Common;
using Finora.Application.Features.Accounts;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public AccountsController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>Returns all visible accounts for the active household.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAccounts(CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var accounts = await _dbContext.Accounts
            .Include(a => a.Household)
            .Where(a => !a.IsArchived &&
                        (a.Visibility == VisibilityLevel.Household ||
                         a.OwnerUserId == userId.Value ||
                         _dbContext.AccessGrants.Any(ag =>
                             ag.ResourceType == "Account" &&
                             ag.ResourceId == a.Id &&
                             ag.GrantedToUserId == userId.Value)))
            .OrderBy(a => a.Name)
            .Select(a => MapToDto(a, userId.Value))
            .ToListAsync(cancellationToken);

        return Ok(accounts);
    }

    /// <summary>Returns a single account. Enforces visibility.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccount(Guid id, CancellationToken cancellationToken)
    {
        var (userId, _) = GetContext();
        if (userId == null) return Unauthorized();

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (account == null) return NotFound();

        if (!CanViewAccount(account, userId.Value))
            return Forbid();

        return Ok(MapToDto(account, userId.Value));
    }

    /// <summary>Creates a new account with an optional opening balance.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Account name is required.", statusCode: 422));

        if (request.OpeningBalancePaise < 0)
            return UnprocessableEntity(Problem("Opening balance cannot be negative.", statusCode: 422));

        var account = new Account
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId.Value,
            OwnerUserId = userId.Value,
            Name = request.Name.Trim(),
            Type = request.Type,
            CurrentBalancePaise = request.OpeningBalancePaise,
            Currency = request.Currency,
            AccountNumberMask = request.AccountNumberMask,
            Visibility = request.Visibility,
            IsArchived = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            householdId.Value, userId.Value,
            "AccountCreated", "Account", account.Id.ToString(),
            newValues: new { account.Name, account.Type, account.CurrentBalancePaise });

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, MapToDto(account, userId.Value));
    }

    /// <summary>Updates account name, mask, visibility, or archived state.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, _) = GetContext();
        if (userId == null) return Unauthorized();

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account == null) return NotFound();

        // Only owner may update
        if (account.OwnerUserId != userId.Value) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Account name is required.", statusCode: 422));

        account.Name = request.Name.Trim();
        account.AccountNumberMask = request.AccountNumberMask;
        account.Visibility = request.Visibility;
        account.IsArchived = request.IsArchived;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            account.HouseholdId, userId.Value,
            "AccountUpdated", "Account", account.Id.ToString(),
            newValues: new { account.Name, account.Visibility, account.IsArchived });

        return Ok(MapToDto(account, userId.Value));
    }

    /// <summary>Soft-archives the account (sets IsArchived = true).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> ArchiveAccount(Guid id, CancellationToken cancellationToken)
    {
        var (userId, _) = GetContext();
        if (userId == null) return Unauthorized();

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account == null) return NotFound();
        if (account.OwnerUserId != userId.Value) return Forbid();

        account.IsArchived = true;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            account.HouseholdId, userId.Value,
            "AccountArchived", "Account", account.Id.ToString());

        return NoContent();
    }

    // ------------------------------------------------------------------
    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);

    private bool CanViewAccount(Account account, Guid userId) =>
        account.Visibility == VisibilityLevel.Household ||
        account.OwnerUserId == userId ||
        _dbContext.AccessGrants.Any(ag =>
            ag.ResourceType == "Account" &&
            ag.ResourceId == account.Id &&
            ag.GrantedToUserId == userId);

    private static AccountDto MapToDto(Account a, Guid currentUserId) => new(
        a.Id,
        a.Name,
        a.Type,
        a.CurrentBalancePaise,
        a.Currency,
        a.AccountNumberMask,
        a.Visibility,
        IsOwner: a.OwnerUserId == currentUserId,
        OwnerFullName: null, // populated via Include when needed
        a.IsArchived,
        a.CreatedAt);
}

