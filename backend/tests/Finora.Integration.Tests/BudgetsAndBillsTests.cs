using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finora.Application.Features.Accounts;
using Finora.Application.Features.Bills;
using Finora.Application.Features.Budgets;
using Finora.Application.Features.Households;
using Finora.Application.Features.Identity;
using Finora.Application.Features.Transactions;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Integration.Tests;

public class BudgetsAndBillsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BudgetsAndBillsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<(string token, Guid householdId)> RegisterAdminAsync(
        string email = "budgetadmin@test.com",
        string householdName = "Budget Family")
    {
        var req = new RegisterInitialAdminRequest(
            "Budget Admin", email, "ComplexPass123!", householdName,
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

    private async Task<Guid> CreateAccountAsync(string token, Guid householdId, string name = "Bank Account")
    {
        var req = Authorized(HttpMethod.Post, "/api/v1/accounts", token, householdId);
        req.Content = JsonContent.Create(new CreateAccountRequest(name, AccountType.Savings, 1_000_000));
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> GetFirstCategoryAsync(string token, Guid householdId)
    {
        var req = Authorized(HttpMethod.Get, "/api/v1/categories?type=Expense", token, householdId);
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var cats = await resp.Content.ReadFromJsonAsync<List<JsonElement>>();
        return Guid.Parse(cats![0].GetProperty("id").GetString()!);
    }

    // ------------------------------------------------------------------
    // Budget tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task SetBudget_And_GetMonthlyBudgets_ShouldCalculateSpendingAndProgress()
    {
        var (token, householdId) = await RegisterAdminAsync("b1@test.com", "BFam1");
        var accountId = await CreateAccountAsync(token, householdId);
        var categoryId = await GetFirstCategoryAsync(token, householdId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Set budget for ₹15,000 (1,500,000 paise)
        var budgetReq = Authorized(HttpMethod.Post, "/api/v1/budgets", token, householdId);
        budgetReq.Content = JsonContent.Create(new SetBudgetRequest(
            categoryId, today.Month, today.Year, 1_500_000));
        var budgetResp = await _client.SendAsync(budgetReq);
        Assert.Equal(HttpStatusCode.OK, budgetResp.StatusCode);

        // 2. Record an expense of ₹5,000 (500,000 paise) in that category
        var txnReq = Authorized(HttpMethod.Post, "/api/v1/transactions", token, householdId);
        txnReq.Content = JsonContent.Create(new CreateTransactionRequest(
            AccountId: accountId,
            CategoryId: categoryId,
            TransactionType: TransactionType.Expense,
            AmountPaise: 500_000,
            Date: today));
        var txnResp = await _client.SendAsync(txnReq);
        txnResp.EnsureSuccessStatusCode();

        // 3. Query monthly budget
        var getReq = Authorized(HttpMethod.Get, $"/api/v1/budgets?month={today.Month}&year={today.Year}", token, householdId);
        var getResp = await _client.SendAsync(getReq);
        getResp.EnsureSuccessStatusCode();

        var summary = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1_500_000, summary.GetProperty("totalAllocatedPaise").GetInt64());
        Assert.Equal(500_000, summary.GetProperty("totalSpentPaise").GetInt64());
        Assert.Equal(33.3, summary.GetProperty("overallPercentageUsed").GetDouble());

        var budgetsList = summary.GetProperty("budgets").EnumerateArray().ToList();
        var catBudget = budgetsList.First(b => b.GetProperty("categoryId").GetString() == categoryId.ToString());
        Assert.Equal(500_000, catBudget.GetProperty("spentAmountPaise").GetInt64());
        Assert.Equal(33.3, catBudget.GetProperty("percentageUsed").GetDouble());
    }

    [Fact]
    public async Task PrivateTransaction_BelongingToMemberA_ShouldBeExcludedFromMemberBBudget()
    {
        // 1. Register Member A (Owner)
        var (adminToken, householdId) = await RegisterAdminAsync("memberA@test.com", "PrivacyBudgetFam");
        var accountId = await CreateAccountAsync(adminToken, householdId);
        var categoryId = await GetFirstCategoryAsync(adminToken, householdId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 2. Set budget of ₹10,000
        var budgetReq = Authorized(HttpMethod.Post, "/api/v1/budgets", adminToken, householdId);
        budgetReq.Content = JsonContent.Create(new SetBudgetRequest(categoryId, today.Month, today.Year, 1_000_000));
        (await _client.SendAsync(budgetReq)).EnsureSuccessStatusCode();

        // 3. Invite Member B
        var inviteReq = Authorized(HttpMethod.Post, $"/api/v1/households/{householdId}/invitations", adminToken, householdId);
        inviteReq.Content = JsonContent.Create(new InviteMemberRequest("memberB@test.com", HouseholdRole.Member));
        var inviteResp = await _client.SendAsync(inviteReq);
        inviteResp.EnsureSuccessStatusCode();
        var inviteToken = (await inviteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("inviteToken").GetString()!;

        // 4. Register Member B
        var regBReq = new RegisterWithInviteRequest("Member B", "ComplexPass123!", inviteToken);
        var regBResp = await _client.PostAsJsonAsync("/api/v1/auth/register-with-invite", regBReq);
        regBResp.EnsureSuccessStatusCode();
        var memberBToken = (await regBResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        // 5. Member A creates a PRIVATE expense transaction of ₹4,000 (400,000 paise)
        var privateTxnReq = Authorized(HttpMethod.Post, "/api/v1/transactions", adminToken, householdId);
        privateTxnReq.Content = JsonContent.Create(new CreateTransactionRequest(
            AccountId: accountId,
            CategoryId: categoryId,
            TransactionType: TransactionType.Expense,
            AmountPaise: 400_000,
            Date: today,
            Visibility: VisibilityLevel.Private));
        (await _client.SendAsync(privateTxnReq)).EnsureSuccessStatusCode();

        // 6. Member A sees their own private transaction in budget: spent = ₹4,000
        var getReqA = Authorized(HttpMethod.Get, $"/api/v1/budgets?month={today.Month}&year={today.Year}", adminToken, householdId);
        var getRespA = await _client.SendAsync(getReqA);
        var summaryA = await getRespA.Content.ReadFromJsonAsync<JsonElement>();
        var budgetA = summaryA.GetProperty("budgets").EnumerateArray()
            .First(b => b.GetProperty("categoryId").GetString() == categoryId.ToString());
        Assert.Equal(400_000, budgetA.GetProperty("spentAmountPaise").GetInt64());

        // 7. Member B queries budget: Member A's private transaction is EXCLUDED: spent = ₹0
        var getReqB = Authorized(HttpMethod.Get, $"/api/v1/budgets?month={today.Month}&year={today.Year}", memberBToken, householdId);
        var getRespB = await _client.SendAsync(getReqB);
        var summaryB = await getRespB.Content.ReadFromJsonAsync<JsonElement>();
        var budgetB = summaryB.GetProperty("budgets").EnumerateArray()
            .First(b => b.GetProperty("categoryId").GetString() == categoryId.ToString());
        Assert.Equal(0, budgetB.GetProperty("spentAmountPaise").GetInt64());
    }

    // ------------------------------------------------------------------
    // Recurring Bill tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateRecurringBill_GeneratesOccurrencesFor60Days()
    {
        var (token, householdId) = await RegisterAdminAsync("b2@test.com", "BFam2");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var billReq = Authorized(HttpMethod.Post, "/api/v1/bills", token, householdId);
        billReq.Content = JsonContent.Create(new CreateRecurringBillRequest(
            Name: "Jio Fiber Broadband",
            ExpectedAmountPaise: 118_000, // ₹1,180
            Frequency: BillFrequency.Monthly,
            NextDueDate: today));

        var billResp = await _client.SendAsync(billReq);
        Assert.Equal(HttpStatusCode.Created, billResp.StatusCode);

        // Fetch occurrences
        var occReq = Authorized(HttpMethod.Get, "/api/v1/bills/occurrences", token, householdId);
        var occResp = await _client.SendAsync(occReq);
        occResp.EnsureSuccessStatusCode();

        var occurrences = await occResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(occurrences);
        Assert.True(occurrences.Count >= 2, "Monthly bill within 60 days should generate at least 2 occurrences");
        Assert.Equal("Jio Fiber Broadband", occurrences[0].GetProperty("billName").GetString());
        Assert.Equal((int)BillOccurrenceStatus.Upcoming, occurrences[0].GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task PayBillOccurrence_CreatesExpenseAndUpdatesOccurrenceAndBalance()
    {
        var (token, householdId) = await RegisterAdminAsync("b3@test.com", "BFam3");
        var accountId = await CreateAccountAsync(token, householdId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get initial balance
        var accBefore = (await (await _client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/accounts/{accountId}", token, householdId)))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentBalancePaise").GetInt64();

        // 1. Create bill
        var billReq = Authorized(HttpMethod.Post, "/api/v1/bills", token, householdId);
        billReq.Content = JsonContent.Create(new CreateRecurringBillRequest(
            Name: "Electricity Bill",
            ExpectedAmountPaise: 250_000, // ₹2,500
            Frequency: BillFrequency.Monthly,
            NextDueDate: today));
        var billResp = await _client.SendAsync(billReq);
        billResp.EnsureSuccessStatusCode();

        // 2. Get first occurrence
        var occReq = Authorized(HttpMethod.Get, "/api/v1/bills/occurrences", token, householdId);
        var occResp = await _client.SendAsync(occReq);
        var occurrences = await occResp.Content.ReadFromJsonAsync<List<JsonElement>>();
        var firstOccId = occurrences![0].GetProperty("id").GetString();

        // 3. Pay occurrence
        var payReq = Authorized(HttpMethod.Post, $"/api/v1/bills/occurrences/{firstOccId}/pay", token, householdId);
        payReq.Content = JsonContent.Create(new PayBillOccurrenceRequest(
            AccountId: accountId,
            PaymentDate: today,
            Notes: "Paid via UPI"));
        var payResp = await _client.SendAsync(payReq);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var paidOcc = await payResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)BillOccurrenceStatus.Paid, paidOcc.GetProperty("status").GetInt32());
        Assert.NotNull(paidOcc.GetProperty("transactionId").GetString());
        Assert.NotNull(paidOcc.GetProperty("paidAt").GetString());

        // 4. Verify account balance was decremented by ₹2,500
        var accAfter = (await (await _client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/accounts/{accountId}", token, householdId)))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentBalancePaise").GetInt64();

        Assert.Equal(accBefore - 250_000, accAfter);
    }

    [Fact]
    public async Task OverdueBill_ShouldBeFlaggedAsOverdue()
    {
        var (token, householdId) = await RegisterAdminAsync("b4@test.com", "BFam4");
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        // Create bill with past due date
        var billReq = Authorized(HttpMethod.Post, "/api/v1/bills", token, householdId);
        billReq.Content = JsonContent.Create(new CreateRecurringBillRequest(
            Name: "Overdue Gym Membership",
            ExpectedAmountPaise: 150_000,
            Frequency: BillFrequency.Monthly,
            NextDueDate: pastDate));
        var billResp = await _client.SendAsync(billReq);
        billResp.EnsureSuccessStatusCode();

        // Fetch occurrences — controller automatically marks past due as Overdue
        var occReq = Authorized(HttpMethod.Get, "/api/v1/bills/occurrences", token, householdId);
        var occResp = await _client.SendAsync(occReq);
        var occurrences = await occResp.Content.ReadFromJsonAsync<List<JsonElement>>();

        var overdueOcc = occurrences!.First(o => o.GetProperty("billName").GetString() == "Overdue Gym Membership");
        Assert.Equal((int)BillOccurrenceStatus.Overdue, overdueOcc.GetProperty("status").GetInt32());
    }
}
