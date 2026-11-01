using Finora.Application.Common;
using Finora.Application.Features.Loans;
using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public LoansController(
        FinoraDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>Lists all loans in the active household.</summary>
    [HttpGet]
    public async Task<IActionResult> ListLoans(CancellationToken cancellationToken)
    {
        var (_, householdId) = GetContext();
        if (householdId == null) return Unauthorized();

        var loans = await _dbContext.Loans
            .Include(l => l.Account)
            .OrderBy(l => l.Name)
            .Select(l => new LoanDto(
                l.Id,
                l.AccountId,
                l.Account != null ? l.Account.Name : "Loan Account",
                l.Name,
                l.OriginalPrincipalPaise,
                l.CurrentPrincipalPaise,
                l.AnnualInterestRate,
                l.TenureMonths,
                l.MonthlyEmiPaise,
                l.StartDate,
                l.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(loans);
    }

    /// <summary>Creates a new loan and provisions its liability account if omitted.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateLoan(
        [FromBody] CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Loan name is required.", statusCode: 422));

        if (request.OriginalPrincipalPaise <= 0)
            return UnprocessableEntity(Problem("Principal must be greater than zero.", statusCode: 422));

        if (request.TenureMonths <= 0)
            return UnprocessableEntity(Problem("Tenure must be greater than zero.", statusCode: 422));

        if (request.AnnualInterestRate < 0)
            return UnprocessableEntity(Problem("Interest rate cannot be negative.", statusCode: 422));

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Guid accountId;
            if (request.AccountId.HasValue)
            {
                var existingAcc = await _dbContext.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.AccountId.Value, cancellationToken);
                if (existingAcc == null)
                    return UnprocessableEntity(Problem("Specified account not found in this household.", statusCode: 422));

                accountId = existingAcc.Id;
            }
            else
            {
                // Auto-create matching liability account
                var loanAccount = new Account
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = householdId.Value,
                    OwnerUserId = userId.Value,
                    Name = $"{request.Name} (Liability)",
                    Type = AccountType.Loan,
                    CurrentBalancePaise = -request.OriginalPrincipalPaise,
                    Currency = "INR",
                    Visibility = VisibilityLevel.Household,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Accounts.Add(loanAccount);
                accountId = loanAccount.Id;
            }

            var emi = Loan.CalculateMonthlyEmi(
                request.OriginalPrincipalPaise,
                request.AnnualInterestRate,
                request.TenureMonths);

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = accountId,
                Name = request.Name.Trim(),
                OriginalPrincipalPaise = request.OriginalPrincipalPaise,
                CurrentPrincipalPaise = request.OriginalPrincipalPaise,
                AnnualInterestRate = request.AnnualInterestRate,
                TenureMonths = request.TenureMonths,
                MonthlyEmiPaise = emi,
                StartDate = request.StartDate,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Loans.Add(loan);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value,
                userId.Value,
                "LoanCreated",
                "Loan",
                loan.Id.ToString(),
                newValues: new { loan.Name, loan.OriginalPrincipalPaise, loan.MonthlyEmiPaise });

            return CreatedAtAction(nameof(GetLoan), new { id = loan.Id }, new LoanDto(
                loan.Id,
                loan.AccountId,
                request.Name,
                loan.Name,
                loan.OriginalPrincipalPaise,
                loan.CurrentPrincipalPaise,
                loan.AnnualInterestRate,
                loan.TenureMonths,
                loan.MonthlyEmiPaise,
                loan.StartDate,
                loan.CreatedAt));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Returns single loan details.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLoan(Guid id, CancellationToken cancellationToken)
    {
        var (_, householdId) = GetContext();
        if (householdId == null) return Unauthorized();

        var loan = await _dbContext.Loans
            .Include(l => l.Account)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (loan == null) return NotFound();

        return Ok(new LoanDto(
            loan.Id,
            loan.AccountId,
            loan.Account?.Name ?? "Loan Account",
            loan.Name,
            loan.OriginalPrincipalPaise,
            loan.CurrentPrincipalPaise,
            loan.AnnualInterestRate,
            loan.TenureMonths,
            loan.MonthlyEmiPaise,
            loan.StartDate,
            loan.CreatedAt));
    }

    /// <summary>Generates the complete reducing-balance amortization schedule.</summary>
    [HttpGet("{id:guid}/amortization")]
    public async Task<IActionResult> GetAmortizationSchedule(Guid id, CancellationToken cancellationToken)
    {
        var (_, householdId) = GetContext();
        if (householdId == null) return Unauthorized();

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan == null) return NotFound();

        var schedule = new List<AmortizationItemDto>();
        long remaining = loan.OriginalPrincipalPaise;
        var date = loan.StartDate;

        for (int m = 1; m <= loan.TenureMonths && remaining > 0; m++)
        {
            var interest = Loan.CalculateMonthlyInterest(remaining, loan.AnnualInterestRate);
            var principal = Math.Min(remaining, loan.MonthlyEmiPaise - interest);
            if (m == loan.TenureMonths || principal > remaining)
            {
                principal = remaining;
            }

            remaining -= principal;

            schedule.Add(new AmortizationItemDto(
                m,
                date,
                principal + interest,
                principal,
                interest,
                remaining));

            date = date.AddMonths(1);
        }

        return Ok(schedule);
    }

    /// <summary>
    /// Records a loan payment.
    /// Invariant: splits payment into principal reduction and interest expenditure.
    /// Only interest counts as finance expense; principal reduces loan liability balance.
    /// </summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordLoanPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (request.PaymentAmountPaise <= 0)
            return UnprocessableEntity(Problem("Payment amount must be greater than zero.", statusCode: 422));

        var loan = await _dbContext.Loans
            .Include(l => l.Account)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan == null) return NotFound();

        var paymentAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.PaymentAccountId, cancellationToken);
        if (paymentAccount == null)
            return UnprocessableEntity(Problem("Payment account not found in this household.", statusCode: 422));

        long interestPaise;
        long principalPaise;

        if (request.CustomPrincipalPaise.HasValue && request.CustomInterestPaise.HasValue)
        {
            if (request.CustomPrincipalPaise.Value + request.CustomInterestPaise.Value != request.PaymentAmountPaise)
                return UnprocessableEntity(Problem("Custom principal and interest must sum to total payment amount.", statusCode: 422));

            principalPaise = request.CustomPrincipalPaise.Value;
            interestPaise = request.CustomInterestPaise.Value;
        }
        else
        {
            // Standard reducing balance calculation for current installment
            interestPaise = Loan.CalculateMonthlyInterest(loan.CurrentPrincipalPaise, loan.AnnualInterestRate);
            if (interestPaise > request.PaymentAmountPaise)
            {
                interestPaise = request.PaymentAmountPaise;
                principalPaise = 0;
            }
            else
            {
                principalPaise = request.PaymentAmountPaise - interestPaise;
            }

            // Cap principal reduction to remaining loan balance
            if (principalPaise > loan.CurrentPrincipalPaise)
            {
                principalPaise = loan.CurrentPrincipalPaise;
            }
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;

            // 1. Record transaction: only interest is an expense to the household P&L
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                AccountId = request.PaymentAccountId,
                CategoryId = null,
                CreatedByUserId = userId.Value,
                TransactionType = TransactionType.Expense,
                AmountPaise = request.PaymentAmountPaise,
                Date = request.PaymentDate,
                Payee = $"{loan.Name} Repayment",
                Notes = request.Notes ?? $"Loan EMI: Principal ₹{principalPaise / 100.0:F2}, Interest ₹{interestPaise / 100.0:F2}",
                Visibility = VisibilityLevel.Household,
                IsSplit = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Transactions.Add(transaction);

            // 2. Decrement payment account balance by full payment amount
            paymentAccount.CurrentBalancePaise -= request.PaymentAmountPaise;
            paymentAccount.UpdatedAt = now;

            // 3. Reduce loan principal balance
            loan.CurrentPrincipalPaise = Math.Max(0, loan.CurrentPrincipalPaise - principalPaise);

            // 4. Update associated liability account balance (liability becomes less negative)
            if (loan.Account != null)
            {
                loan.Account.CurrentBalancePaise += principalPaise;
                loan.Account.UpdatedAt = now;
            }

            // 5. Create loan payment record
            var payment = new LoanPayment
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId.Value,
                LoanId = loan.Id,
                TransactionId = transaction.Id,
                PrincipalPaise = principalPaise,
                InterestPaise = interestPaise,
                PaymentDate = request.PaymentDate
            };

            _dbContext.LoanPayments.Add(payment);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogAsync(
                householdId.Value,
                userId.Value,
                "LoanPaymentRecorded",
                "LoanPayment",
                payment.Id.ToString(),
                newValues: new { loan.Name, request.PaymentAmountPaise, principalPaise, interestPaise, RemainingPrincipal = loan.CurrentPrincipalPaise });

            return Ok(new LoanPaymentDto(
                payment.Id,
                payment.LoanId,
                payment.TransactionId,
                payment.PrincipalPaise,
                payment.InterestPaise,
                payment.PaymentDate));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Deletes a loan.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLoan(Guid id, CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan == null) return NotFound();

        _dbContext.Loans.Remove(loan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(householdId.Value, userId.Value, "LoanDeleted", "Loan", id.ToString());

        return NoContent();
    }

    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);
}

