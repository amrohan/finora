using Finora.Domain.Enums;

namespace Finora.Application.Features.Categories;

public record CategoryDto(
    Guid Id,
    Guid? ParentCategoryId,
    string Name,
    TransactionType Type,
    string? Icon,
    string? ColorHex,
    bool IsSystem,
    IReadOnlyList<CategoryDto>? SubCategories = null);

public record CreateCategoryRequest(
    string Name,
    TransactionType Type = TransactionType.Expense,
    Guid? ParentCategoryId = null,
    string? Icon = null,
    string? ColorHex = null);

