using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finora.Application.Features.Accounts;
using Finora.Application.Features.Goals;
using Finora.Application.Features.Identity;
using Finora.Application.Features.Loans;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Integration.Tests;

public class LoansAndGoalsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoansAndGoalsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<(string token, Guid householdId)> RegisterAdminAsync(
        string email = "wealthadmin@test.com",
        string householdName = "Wealth Family")
    {
        var req = new RegisterInitialAdminRequest(
            "Wealth Admin", email, "ComplexPass123!", householdName,
            "dev_initial_setup_token_finora_2026");

        var resp = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var householdId = Guid.Parse(body.GetProperty("activeHouseholdId").GetString()!);
        return (token, householdId);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, Guid householdId)
    {
        var msg = new HttpRequestMessage(method, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.Add("X-Household-Id", householdId.ToString());
        return msg;
    }

    private async Task<Guid> CreateAccountAsync(string token, Guid householdId, string name = "HDFC Savings", long balance = 10_000_000)
    {
        var req = Authorized(HttpMethod.Post, "/api/v1/accounts", token, householdId);
        req.Content = JsonContent.Create(new CreateAccountRequest(name, AccountType.Savings, balance));
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ------------------------------------------------------------------
    // Loan tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateLoan_And_GetAmortization_ShouldReturnSchedule()
    {
        var (token, householdId) = await RegisterAdminAsync("loan1@test.com", "LFam1");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Loan of ₹5,00,000 (50,000,000 paise), 10% rate, 36 months
        var loanReq = Authorized(HttpMethod.Post, "/api/v1/loans", token, householdId);
        loanReq.Content = JsonContent.Create(new CreateLoanRequest(
            Name: "Car Loan HDFC",
            OriginalPrincipalPaise: 50_000_000,
            AnnualInterestRate: 10.00m,
            TenureMonths: 36,
            StartDate: today));

        var loanResp = await _client.SendAsync(loanReq);
        Assert.Equal(HttpStatusCode.Created, loanResp.StatusCode);

        var loanBody = await loanResp.Content.ReadFromJsonAsync<JsonElement>();
        var loanId = loanBody.GetProperty("id").GetString();
        Assert.True(loanBody.GetProperty("monthlyEmiPaise").GetInt64() > 0);

        // Fetch amortization schedule
        var amortReq = Authorized(HttpMethod.Get, $"/api/v1/loans/{loanId}/amortization", token, householdId);
        var amortResp = await _client.SendAsync(amortReq);
        amortResp.EnsureSuccessStatusCode();

        var schedule = await amortResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(schedule);
        Assert.Equal(36, schedule.Count);
        Assert.Equal(0, schedule[^1].GetProperty("remainingPrincipalPaise").GetInt64());
    }

    [Fact]
    public async Task RecordLoanPayment_ReducesPrincipalAndRecordsInterestPortion()
    {
        var (token, householdId) = await RegisterAdminAsync("loan2@test.com", "LFam2");
        var bankAccId = await CreateAccountAsync(token, householdId, "Payment Bank", 10_000_000); // ₹1,00,000
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Loan: ₹1,00,000 (10,000,000 paise) at 12% p.a.
        var loanReq = Authorized(HttpMethod.Post, "/api/v1/loans", token, householdId);
        loanReq.Content = JsonContent.Create(new CreateLoanRequest(
            Name: "Personal Loan",
            OriginalPrincipalPaise: 10_000_000,
            AnnualInterestRate: 12.00m,
            TenureMonths: 12,
            StartDate: today));
        var loanResp = await _client.SendAsync(loanReq);
        loanResp.EnsureSuccessStatusCode();
        var loanId = (await loanResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Record payment of ₹10,000 (1,000,000 paise)
        // At 12% p.a., monthly interest on ₹1,00,000 = 1% = ₹1,000 (100,000 paise)
        // Principal reduction = ₹10,000 - ₹1,000 = ₹9,000 (900,000 paise)
        var payReq = Authorized(HttpMethod.Post, $"/api/v1/loans/{loanId}/payments", token, householdId);
        payReq.Content = JsonContent.Create(new RecordLoanPaymentRequest(
            PaymentAccountId: bankAccId,
            PaymentAmountPaise: 1_000_000,
            PaymentDate: today,
            Notes: "EMI Payment"));
        var payResp = await _client.SendAsync(payReq);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var payBody = await payResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(900_000, payBody.GetProperty("principalPaise").GetInt64());
        Assert.Equal(100_000, payBody.GetProperty("interestPaise").GetInt64());

        // Verify remaining principal on loan is ₹91,000 (9,100,000 paise)
        var getLoanResp = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/loans/{loanId}", token, householdId));
        var updatedLoan = await getLoanResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(9_100_000, updatedLoan.GetProperty("currentPrincipalPaise").GetInt64());

        // Verify bank balance was debited by full payment amount (₹10,000)
        var getBankResp = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/accounts/{bankAccId}", token, householdId));
        var updatedBank = await getBankResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(9_000_000, updatedBank.GetProperty("currentBalancePaise").GetInt64());
    }

    // ------------------------------------------------------------------
    // Savings Goal tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task ContributeToSavingsGoal_IncrementsGoalBalance_And_DoesNotIncreaseBudgetExpenses()
    {
        var (token, householdId) = await RegisterAdminAsync("goal1@test.com", "GFam1");
        var bankAccId = await CreateAccountAsync(token, householdId, "Savings Account", 10_000_000); // ₹1,00,000
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Create Savings Goal for ₹1,00,000 (10,000,000 paise)
        var goalReq = Authorized(HttpMethod.Post, "/api/v1/goals", token, householdId);
        goalReq.Content = JsonContent.Create(new CreateSavingsGoalRequest(
            Name: "Emergency Fund",
            TargetAmountPaise: 10_000_000));
        var goalResp = await _client.SendAsync(goalReq);
        Assert.Equal(HttpStatusCode.Created, goalResp.StatusCode);
        var goalId = (await goalResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // 2. Contribute ₹5,000 (500,000 paise)
        var contribReq = Authorized(HttpMethod.Post, $"/api/v1/goals/{goalId}/contributions", token, householdId);
        contribReq.Content = JsonContent.Create(new ContributeToGoalRequest(
            SourceAccountId: bankAccId,
            AmountPaise: 500_000,
            ContributionDate: today,
            Notes: "Monthly allocation"));
        var contribResp = await _client.SendAsync(contribReq);
        Assert.Equal(HttpStatusCode.OK, contribResp.StatusCode);

        // 3. Verify goal balance is ₹5,000
        var listGoalsResp = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/goals", token, householdId));
        var goals = await listGoalsResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var targetGoal = goals!.First(g => g.GetProperty("id").GetString() == goalId);
        Assert.Equal(500_000, targetGoal.GetProperty("currentAmountPaise").GetInt64());
        Assert.False(targetGoal.GetProperty("isCompleted").GetBoolean());

        // 4. Verify bank account debited by ₹5,000
        var bankResp = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/accounts/{bankAccId}", token, householdId));
        var bankBody = await bankResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(9_500_000, bankBody.GetProperty("currentBalancePaise").GetInt64());

        // 5. CRITICAL INVARIANT: Goal contribution must NOT count as an expense in monthly budgets
        var budgetResp = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/budgets?month={today.Month}&year={today.Year}", token, householdId));
        var budgetSummary = await budgetResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, budgetSummary.GetProperty("totalSpentPaise").GetInt64());
    }

    [Fact]
    public async Task ContributeToGoal_ReachingTarget_MarksGoalCompleted()
    {
        var (token, householdId) = await RegisterAdminAsync("goal2@test.com", "GFam2");
        var bankAccId = await CreateAccountAsync(token, householdId, "Funding Account", 5_000_000);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Goal: ₹10,000 target
        var goalReq = Authorized(HttpMethod.Post, "/api/v1/goals", token, householdId);
        goalReq.Content = JsonContent.Create(new CreateSavingsGoalRequest(
            Name: "Vacation Fund",
            TargetAmountPaise: 1_000_000));
        var goalResp = await _client.SendAsync(goalReq);
        var goalId = (await goalResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Contribute full ₹10,000
        var contribReq = Authorized(HttpMethod.Post, $"/api/v1/goals/{goalId}/contributions", token, householdId);
        contribReq.Content = JsonContent.Create(new ContributeToGoalRequest(
            SourceAccountId: bankAccId,
            AmountPaise: 1_000_000,
            ContributionDate: today));
        (await _client.SendAsync(contribReq)).EnsureSuccessStatusCode();

        // Verify goal marked completed
        var listGoalsResp = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/goals", token, householdId));
        var goals = await listGoalsResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var targetGoal = goals!.First(g => g.GetProperty("id").GetString() == goalId);
        Assert.True(targetGoal.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(100.0, targetGoal.GetProperty("progressPercentage").GetDouble());
    }
}

