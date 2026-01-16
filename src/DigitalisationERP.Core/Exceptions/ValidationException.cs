namespace DigitalisationERP.Core.Exceptions;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : DomainException
{
    public string PropertyName { get; }

    public ValidationException(string propertyName, string message) : base(message)
    {
        PropertyName = propertyName;
    }

    public ValidationException(string message) : base(message)
    {
        PropertyName = string.Empty;
    }

    public ValidationException()
    {
        PropertyName = string.Empty;
    }

    public ValidationException(string message, Exception innerException) : base(message, innerException)
    {
        PropertyName = string.Empty;
    }
}
