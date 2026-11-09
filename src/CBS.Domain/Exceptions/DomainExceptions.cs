namespace CBS.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class InvariantViolationException : DomainException
{
    public string InvariantName { get; }
    public string RegulatoryReference { get; }

    public InvariantViolationException(string invariantName, string regulatoryReference, string message)
        : base($"[INVARIANT VIOLATION: {invariantName}] ({regulatoryReference}): {message}")
    {
        InvariantName = invariantName;
        RegulatoryReference = regulatoryReference;
    }
}
