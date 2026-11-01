using Finora.Application.Common;
using Finora.Application.Features.Categories;
using Finora.Domain.Entities;
using Finora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finora.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly FinoraDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CategoriesController(FinoraDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns flat list of categories for the household. Optionally filter by type.
    /// Pass ?nested=true to receive tree structure with SubCategories populated.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListCategories(
        [FromQuery] string? type = null,
        [FromQuery] bool nested = false,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.HouseholdId == null) return Unauthorized();

        var query = _dbContext.Categories.AsQueryable();

        if (!string.IsNullOrEmpty(type) &&
            Enum.TryParse<Domain.Enums.TransactionType>(type, ignoreCase: true, out var parsedType))
        {
            query = query.Where(c => c.Type == parsedType);
        }

        var all = await query
            .OrderBy(c => c.IsSystem ? 0 : 1)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (!nested)
        {
            return Ok(all.Select(c => MapToDto(c, includeChildren: false)));
        }

        // Build tree: roots only (no parent), children embedded
        var lookup = all.ToLookup(c => c.ParentCategoryId);
        var roots = lookup[null].Select(c => MapToDto(c, includeChildren: true, lookup)).ToList();
        return Ok(roots);
    }

    /// <summary>Creates a new custom category for the household.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, householdId) = GetContext();
        if (userId == null || householdId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return UnprocessableEntity(Problem("Category name is required.", statusCode: 422));

        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == request.ParentCategoryId.Value, cancellationToken);
            if (!parentExists)
                return UnprocessableEntity(Problem("Parent category not found.", statusCode: 422));
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId.Value,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Icon = request.Icon,
            ColorHex = request.ColorHex,
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ListCategories), MapToDto(category, includeChildren: false));
    }

    /// <summary>Deletes a custom (non-system) category that has no transactions.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUserService.HouseholdId == null) return Unauthorized();

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null) return NotFound();

        if (category.IsSystem)
            return UnprocessableEntity(Problem("System categories cannot be deleted.", statusCode: 422));

        var hasTransactions = await _dbContext.Transactions
            .AnyAsync(t => t.CategoryId == id, cancellationToken);

        if (hasTransactions)
            return UnprocessableEntity(Problem(
                "Cannot delete a category that has transactions. Reassign transactions first.", statusCode: 422));

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ------------------------------------------------------------------
    private (Guid? userId, Guid? householdId) GetContext() =>
        (_currentUserService.UserId, _currentUserService.HouseholdId);

    private static CategoryDto MapToDto(
        Category c,
        bool includeChildren,
        ILookup<Guid?, Category>? lookup = null)
    {
        IReadOnlyList<CategoryDto>? subs = null;
        if (includeChildren && lookup != null)
        {
            subs = lookup[c.Id]
                .Select(child => MapToDto(child, includeChildren: true, lookup))
                .ToList();
        }

        return new CategoryDto(c.Id, c.ParentCategoryId, c.Name, c.Type, c.Icon, c.ColorHex, c.IsSystem, subs);
    }
}

