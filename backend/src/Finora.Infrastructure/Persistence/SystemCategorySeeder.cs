using Finora.Domain.Entities;
using Finora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finora.Infrastructure.Persistence;

/// <summary>
/// Seeds default INR categories for a newly created household.
/// Called after household creation — not from migrations (categories are per-household).
/// </summary>
public static class SystemCategorySeeder
{
    private static readonly (string Name, TransactionType Type, string? Icon, string ColorHex)[] DefaultCategories =
    [
        // Expense categories
        ("Groceries",       TransactionType.Expense, "🛒", "#4CAF50"),
        ("Utilities",       TransactionType.Expense, "💡", "#FF9800"),
        ("Housing",         TransactionType.Expense, "🏠", "#9C27B0"),
        ("Transportation",  TransactionType.Expense, "🚗", "#2196F3"),
        ("Healthcare",      TransactionType.Expense, "🏥", "#F44336"),
        ("Education",       TransactionType.Expense, "📚", "#3F51B5"),
        ("Entertainment",   TransactionType.Expense, "🎬", "#E91E63"),
        ("Personal Care",   TransactionType.Expense, "💆", "#00BCD4"),
        ("Dining Out",      TransactionType.Expense, "🍽️", "#FF5722"),
        ("Shopping",        TransactionType.Expense, "🛍️", "#607D8B"),
        ("Insurance",       TransactionType.Expense, "🛡️", "#795548"),
        ("Taxes",           TransactionType.Expense, "📄", "#9E9E9E"),
        ("Miscellaneous",   TransactionType.Expense, "📦", "#BDBDBD"),

        // Income categories
        ("Salary",              TransactionType.Income, "💼", "#4CAF50"),
        ("Freelance",           TransactionType.Income, "💻", "#8BC34A"),
        ("Investment Returns",  TransactionType.Income, "📈", "#CDDC39"),
        ("Rental Income",       TransactionType.Income, "🏘️", "#FFEB3B"),
        ("Other Income",        TransactionType.Income, "💰", "#FFC107"),

        // Transfer (internal — excluded from P&L)
        ("Account Transfer",    TransactionType.Transfer, "🔄", "#607D8B"),
    ];

    public static async Task SeedAsync(
        FinoraDbContext dbContext,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var hasAny = await dbContext.Categories
            .AnyAsync(c => c.HouseholdId == householdId && c.IsSystem, cancellationToken);

        if (hasAny) return;

        var now = DateTimeOffset.UtcNow;
        var categories = DefaultCategories.Select(c => new Category
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            Name = c.Name,
            Type = c.Type,
            Icon = c.Icon,
            ColorHex = c.ColorHex,
            IsSystem = true,
            CreatedAt = now
        }).ToList();

        dbContext.Categories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

