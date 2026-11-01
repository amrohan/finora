using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finora.Application.Features.Households;
using Finora.Application.Features.Identity;
using Finora.Domain.Enums;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finora.Integration.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "Finora_TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DbContext with an isolated in-memory database per test run
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FinoraDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<FinoraDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });
    }
}

public class TenancyAndAuthIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public TenancyAndAuthIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task RegisterInitialAdmin_WithInvalidToken_ShouldReturnUnauthorized()
    {
        var request = new RegisterInitialAdminRequest(
            "Admin User",
            "fakeadmin@example.com",
            "Password123!",
            "My Family",
            "wrong_token");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteWorkflow_RegisterAdmin_InviteMember_VerifyTenancyIsolation()
    {
        // 1. Register Initial Admin
        var adminRequest = new RegisterInitialAdminRequest(
            "Rohan Sharma",
            "admin@sharmafamily.com",
            "ComplexPass123!",
            "Sharma Household",
            "dev_initial_setup_token_finora_2026");

        var adminResponse = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var adminAuth = await adminResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(adminAuth);
        Assert.NotEqual(Guid.Empty, adminAuth.ActiveHouseholdId);
        Assert.Equal(HouseholdRole.Owner, adminAuth.Role);

        var householdId = adminAuth.ActiveHouseholdId!.Value;

        // Set Auth header for admin
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        _client.DefaultRequestHeaders.Remove("X-Household-Id");
        _client.DefaultRequestHeaders.Add("X-Household-Id", householdId.ToString());

        // 2. Admin invites a family member
        var inviteRequest = new InviteMemberRequest("priya@sharmafamily.com", HouseholdRole.Member);
        var inviteResponse = await _client.PostAsJsonAsync($"/api/v1/households/{householdId}/invitations", inviteRequest);
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);

        var inviteDto = await inviteResponse.Content.ReadFromJsonAsync<InvitationDto>();
        Assert.NotNull(inviteDto);
        Assert.NotNull(inviteDto.InviteToken);

        // 3. Member registers with invite token
        var memberRegisterRequest = new RegisterWithInviteRequest(
            "Priya Sharma",
            "MemberPass123!",
            inviteDto.InviteToken);

        // Remove auth header for public registration
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("X-Household-Id");

        var memberResponse = await _client.PostAsJsonAsync("/api/v1/auth/register-with-invite", memberRegisterRequest);
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);

        var memberAuth = await memberResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(memberAuth);
        Assert.Equal(householdId, memberAuth.ActiveHouseholdId);
        Assert.Equal(HouseholdRole.Member, memberAuth.Role);

        // 4. Create an unrelated separate household for a third party
        var unrelatedAdminRequest = new RegisterInitialAdminRequest(
            "Ananya Verma",
            "ananya@vermafamily.com",
            "VermaPass123!",
            "Verma Household",
            "dev_initial_setup_token_finora_2026");

        var unrelatedResponse = await _client.PostAsJsonAsync("/api/v1/auth/register-initial", unrelatedAdminRequest);
        Assert.Equal(HttpStatusCode.OK, unrelatedResponse.StatusCode);
        var unrelatedAuth = await unrelatedResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(unrelatedAuth);
        var unrelatedHouseholdId = unrelatedAuth.ActiveHouseholdId!.Value;

        // 5. Tenancy Isolation Test:
        // Sharma family member attempts to access Verma Household
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberAuth.AccessToken);
        _client.DefaultRequestHeaders.Remove("X-Household-Id");
        _client.DefaultRequestHeaders.Add("X-Household-Id", unrelatedHouseholdId.ToString());

        // Attempting to query unrelated household with X-Household-Id header must be blocked by TenancyMiddleware
        var forbiddenAccessResponse = await _client.GetAsync($"/api/v1/households/{unrelatedHouseholdId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAccessResponse.StatusCode);
    }
}

