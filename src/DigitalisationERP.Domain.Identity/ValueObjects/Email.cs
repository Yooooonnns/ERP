using DigitalisationERP.Core.Exceptions;

namespace DigitalisationERP.Domain.Identity.ValueObjects;

/// <summary>
/// Value object representing an email address with validation rules.
/// </summary>
public class Email : IEquatable<Email>
{
    /// <summary>
    /// Gets the email address value.
    /// </summary>
    public string Value { get; private set; }

    private const int MaxLength = 256;

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Email value object with validation.
    /// </summary>
    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(Email), "Email cannot be empty");

        if (value.Length > MaxLength)
            throw new ValidationException(nameof(Email), $"Email cannot exceed {MaxLength} characters");

        // Simple email validation
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, 
            @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            throw new ValidationException(nameof(Email), "Email format is invalid");

        return new Email(value.ToLower());
    }

    public bool Equals(Email? other) => other != null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Email);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
