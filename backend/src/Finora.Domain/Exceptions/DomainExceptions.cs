namespace Finora.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class TenancyViolationException : DomainException
{
    public TenancyViolationException(string message) : base(message) { }
}

public class InvalidInvitationException : DomainException
{
    public InvalidInvitationException(string message) : base(message) { }
}

public class UnauthorizedFinancialAccessException : DomainException
{
    public UnauthorizedFinancialAccessException(string message) : base(message) { }
}

