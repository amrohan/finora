using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Xunit;

namespace Finora.Domain.Tests;

public class HouseholdAndInvitationTests
{
    [Fact]
    public void Household_ShouldDefaultToInrAndAsiaKolkata()
    {
        var household = new Household
        {
            Name = "Kapoor Family",
            CreatedByUserId = Guid.NewGuid()
        };

        Assert.Equal("INR", household.Currency);
        Assert.Equal("Asia/Kolkata", household.Timezone);
    }

    [Fact]
    public void Invitation_WhenPendingAndNotExpired_ShouldBeValid()
    {
        var invitation = new Invitation
        {
            HouseholdId = Guid.NewGuid(),
            Email = "member@example.com",
            Role = HouseholdRole.Member,
            TokenHash = "abc123hash",
            InvitedByUserId = Guid.NewGuid(),
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        Assert.True(invitation.IsValid);
    }

    [Fact]
    public void Invitation_WhenExpired_ShouldNotBeValid()
    {
        var invitation = new Invitation
        {
            HouseholdId = Guid.NewGuid(),
            Email = "member@example.com",
            Role = HouseholdRole.Member,
            TokenHash = "abc123hash",
            InvitedByUserId = Guid.NewGuid(),
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Assert.False(invitation.IsValid);
    }

    [Fact]
    public void Invitation_WhenAccepted_ShouldNotBeValid()
    {
        var invitation = new Invitation
        {
            HouseholdId = Guid.NewGuid(),
            Email = "member@example.com",
            Role = HouseholdRole.Member,
            TokenHash = "abc123hash",
            InvitedByUserId = Guid.NewGuid(),
            Status = InvitationStatus.Accepted,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(5)
        };

        Assert.False(invitation.IsValid);
    }
}

