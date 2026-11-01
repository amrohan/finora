using Finora.Api.Services;
using Finora.Application.Common;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Middleware;

public class TenancyMiddleware
{
    private readonly RequestDelegate _next;

    public TenancyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUserService,
        FinoraDbContext dbContext)
    {
        if (currentUserService.IsAuthenticated && currentUserService.UserId.HasValue)
        {
            var userId = currentUserService.UserId.Value;

            if (context.Request.Headers.TryGetValue("X-Household-Id", out var householdHeader) &&
                Guid.TryParse(householdHeader, out var requestedHouseholdId))
            {
                // Verify user belongs to requested household
                var membership = await dbContext.HouseholdMembers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.HouseholdId == requestedHouseholdId && m.UserId == userId);

                if (membership == null)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "You do not have access to the requested household."
                    };

                    await context.Response.WriteAsJsonAsync(problem);
                    return;
                }

                if (currentUserService is CurrentUserService concreteService)
                {
                    concreteService.SetTenant(membership.HouseholdId, membership.Role);
                }
            }
            else if (currentUserService.HouseholdId.HasValue)
            {
                // Verify claims household
                var membership = await dbContext.HouseholdMembers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.HouseholdId == currentUserService.HouseholdId.Value && m.UserId == userId);

                if (membership != null && currentUserService is CurrentUserService concreteService)
                {
                    concreteService.SetTenant(membership.HouseholdId, membership.Role);
                }
            }
        }

        await _next(context);
    }
}

