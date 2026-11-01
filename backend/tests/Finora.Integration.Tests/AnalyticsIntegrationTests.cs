using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finora.Application.Features.Accounts;
using Finora.Application.Features.Analytics;
using Finora.Application.Features.Households;
using Finora.Application.Features.Identity;
using Finora.Application.Features.Transactions;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Integration.Tests;

public class AnalyticsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnalyticsIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string token, Guid householdId)> RegisterAdminAsync(
        string email = "analyticsadmin@test.com",
        string householdName = "Analytics Family")
    {
        var req = new RegisterInitialAdminRequest(
            "Analytics Admin", email, "ComplexPass123!", householdName,
            "dev_initial_setup_token_finora_2026");

        var resp = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", req);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var householdId = Guid.Parse(body.GetProperty("activeHouseholdId").GetString()!);
        return (token, householdId);
    }

    private async Task<string> InviteAndRegisterMemberAsync(
        string adminToken,
        Guid householdId,
        string memberEmail,
        string memberName)
    {
        // 1. Invite
        var inviteReq = Authorized(HttpMethod.Post, $"/api/v1/households/{householdId}/invitations", adminToken, householdId);
        inviteReq.Content = JsonContent.Create(new InviteMemberRequest(memberEmail, HouseholdRole.Member));
        var inviteResp = await _client.SendAsync(inviteReq);
        inviteResp.EnsureSuccessStatusCode();
        var inviteBody = await inviteResp.Content.ReadFromJsonAsync<JsonElement>();
        var inviteToken = inviteBody.GetProperty("inviteToken").GetString()!;

        // 2. Accept invite
        var acceptReq = new RegisterWithInviteRequest(memberName, "MemberPass123!", inviteToken);
        var acceptResp = await _client.PostAsJsonAsync("/api/v1/auth/register-with-invite", acceptReq);
        acceptResp.EnsureSuccessStatusCode();
        var acceptBody = await acceptResp.Content.ReadFromJsonAsync<JsonElement>();
        return acceptBody.GetProperty("accessToken").GetString()!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, Guid householdId)
    {
        var msg = new HttpRequestMessage(method, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.Add("X-Household-Id", householdId.ToString());
        return msg;
    }

    private async Task<Guid> CreateAccountAsync(
        string token,
        Guid householdId,
        string name,
        AccountType type,
        long balancePaise,
        VisibilityLevel visibility = VisibilityLevel.Household)
    {
        var req = Authorized(HttpMethod.Post, "/api/v1/accounts", token, householdId);
        req.Content = JsonContent.Create(new CreateAccountRequest(name, type, balancePaise, "INR", null, visibility));
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetOverview_ComputesNetWorth_WithStrictPrivacyIsolation()
    {
        var (adminToken, householdId) = await RegisterAdminAsync("nw_admin@test.com", "NW Fam");
        var memberToken = await InviteAndRegisterMemberAsync(adminToken, householdId, "nw_member@test.com", "NW Member");

        // 1. Admin creates a SHARED savings account: ₹1,00,000 (10,000,000 paise)
        await CreateAccountAsync(adminToken, householdId, "Shared HDFC", AccountType.Savings, 10_000_000, VisibilityLevel.Household);

        // 2. Member creates a PRIVATE savings account: ₹50,000 (5,000,000 paise)
        await CreateAccountAsync(memberToken, householdId, "Member Private ICICI", AccountType.Savings, 5_000_000, VisibilityLevel.Private);

        // 3. Admin requests Net Worth Overview
        var adminReq = Authorized(HttpMethod.Get, "/api/v1/analytics/overview", adminToken, householdId);
        var adminResp = await _client.SendAsync(adminReq);
        adminResp.EnsureSuccessStatusCode();
        var adminOverview = await adminResp.Content.ReadFromJsonAsync<NetWorthOverviewDto>();

        Assert.NotNull(adminOverview);
        // Admin must only see ₹1,00,000 (Member's ₹50,000 is private!)
        Assert.Equal(10_000_000L, adminOverview.TotalAssetsPaise);
        Assert.Equal(10_000_000L, adminOverview.NetWorthPaise);

        // 4. Member requests Net Worth Overview
        var memberReq = Authorized(HttpMethod.Get, "/api/v1/analytics/overview", memberToken, householdId);
        var memberResp = await _client.SendAsync(memberReq);
        memberResp.EnsureSuccessStatusCode();
        var memberOverview = await memberResp.Content.ReadFromJsonAsync<NetWorthOverviewDto>();

        Assert.NotNull(memberOverview);
        // Member sees Shared ₹1,00,000 + their own Private ₹50,000 = ₹1,50,000
        Assert.Equal(15_000_000L, memberOverview.TotalAssetsPaise);
        Assert.Equal(15_000_000L, memberOverview.NetWorthPaise);
    }

    [Fact]
    public async Task SpendingBreakdown_ExcludesTransfers_AndAggregatesCategories()
    {
        var (token, householdId) = await RegisterAdminAsync("breakdown_admin@test.com", "Breakdown Fam");
        var acc1 = await CreateAccountAsync(token, householdId, "Bank A", AccountType.Checking, 50_000_000);
        var acc2 = await CreateAccountAsync(token, householdId, "Bank B", AccountType.Savings, 10_000_000);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Record ₹2,500 Expense
        var expReq = Authorized(HttpMethod.Post, "/api/v1/transactions", token, householdId);
        expReq.Content = JsonContent.Create(new CreateTransactionRequest(
            acc1, null, TransactionType.Expense, 250_000, today, "Supermarket", "Groceries"));
        var expResp = await _client.SendAsync(expReq);
        expResp.EnsureSuccessStatusCode();

        // Record ₹1,000 Transfer between Bank A and Bank B
        var trfReq = Authorized(HttpMethod.Post, "/api/v1/transactions/transfers", token, householdId);
        trfReq.Content = JsonContent.Create(new RecordTransferRequest(
            acc1, acc2, 100_000, today, "Transfer to savings"));
        var trfResp = await _client.SendAsync(trfReq);
        trfResp.EnsureSuccessStatusCode();

        // Request spending breakdown
        var breakdownReq = Authorized(HttpMethod.Get, $"/api/v1/analytics/spending-breakdown?month={today.Month}&year={today.Year}", token, householdId);
        var breakdownResp = await _client.SendAsync(breakdownReq);
        breakdownResp.EnsureSuccessStatusCode();

        var breakdown = await breakdownResp.Content.ReadFromJsonAsync<SpendingBreakdownDto>();
        Assert.NotNull(breakdown);

        // Expense must be exactly ₹2,500 (250,000 paise). The ₹1,000 transfer is NOT an expense!
        Assert.Equal(250_000L, breakdown.TotalExpensePaise);
        Assert.Single(breakdown.TopPayees);
        Assert.Equal("Supermarket", breakdown.TopPayees[0].Payee);
        Assert.Equal(250_000L, breakdown.TopPayees[0].AmountPaise);
    }

    [Fact]
    public async Task ExportTransactions_CsvAndJson_RespectsPrivacyAndOutputsProperHeaders()
    {
        var (adminToken, householdId) = await RegisterAdminAsync("export_admin@test.com", "Export Fam");
        var memberToken = await InviteAndRegisterMemberAsync(adminToken, householdId, "export_mem@test.com", "Export Member");

        var sharedAcc = await CreateAccountAsync(adminToken, householdId, "Shared Checking", AccountType.Checking, 50_000_000);
        var memberPrivateAcc = await CreateAccountAsync(memberToken, householdId, "Mem Secret Acc", AccountType.Savings, 10_000_000, VisibilityLevel.Private);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Admin records shared expense
        var adminTx = Authorized(HttpMethod.Post, "/api/v1/transactions", adminToken, householdId);
        adminTx.Content = JsonContent.Create(new CreateTransactionRequest(
            sharedAcc, null, TransactionType.Expense, 150_000, today, "Shared Vendor", "Office"));
        (await _client.SendAsync(adminTx)).EnsureSuccessStatusCode();

        // Member records private expense
        var memberTx = Authorized(HttpMethod.Post, "/api/v1/transactions", memberToken, householdId);
        memberTx.Content = JsonContent.Create(new CreateTransactionRequest(
            memberPrivateAcc, null, TransactionType.Expense, 80_000, today, "Secret Vendor", "Personal", VisibilityLevel.Private));
        (await _client.SendAsync(memberTx)).EnsureSuccessStatusCode();

        // 1. Admin exports CSV
        var csvReq = Authorized(HttpMethod.Get, "/api/v1/analytics/export/transactions?format=csv", adminToken, householdId);
        var csvResp = await _client.SendAsync(csvReq);
        csvResp.EnsureSuccessStatusCode();
        Assert.Equal("text/csv; charset=utf-8", csvResp.Content.Headers.ContentType?.ToString());

        var csvContent = await csvResp.Content.ReadAsStringAsync();
        Assert.Contains("Date,Type,Payee,Category,Account,Amount (INR),Notes,Visibility", csvContent);
        Assert.Contains("Shared Vendor", csvContent);
        // Admin must NOT see Member's secret transaction
        Assert.DoesNotContain("Secret Vendor", csvContent);

        // 2. Admin exports JSON
        var jsonReq = Authorized(HttpMethod.Get, "/api/v1/analytics/export/transactions?format=json", adminToken, householdId);
        var jsonResp = await _client.SendAsync(jsonReq);
        jsonResp.EnsureSuccessStatusCode();
        Assert.Equal("application/json", jsonResp.Content.Headers.ContentType?.MediaType);

        var jsonContent = await jsonResp.Content.ReadAsStringAsync();
        Assert.Contains("Shared Vendor", jsonContent);
        Assert.DoesNotContain("Secret Vendor", jsonContent);
    }
}
