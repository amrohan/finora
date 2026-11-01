namespace Finora.Domain.Common;

public interface IEntity
{
    Guid Id { get; }
}

public interface ITenantScoped
{
    Guid HouseholdId { get; set; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

