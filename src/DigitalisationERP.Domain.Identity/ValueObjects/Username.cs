using DigitalisationERP.Core.Exceptions;

namespace DigitalisationERP.Domain.Identity.ValueObjects;

/// <summary>
/// Value object representing a username with validation rules.
/// </summary>
public class Username : IEquatable<Username>
{
    /// <summary>
    /// Gets the username value.
    /// </summary>
    public string Value { get; private set; }

    private const int MinLength = 3;
    private const int MaxLength = 128;

    private Username(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Username value object with validation.
    /// </summary>
    public static Username Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(Username), "Username cannot be empty");

        if (value.Length < MinLength || value.Length > MaxLength)
            throw new ValidationException(nameof(Username), 
                $"Username must be between {MinLength} and {MaxLength} characters");

        // Only allow alphanumeric, dots, underscores, hyphens
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9._-]+$"))
            throw new ValidationException(nameof(Username), 
                "Username can only contain alphanumeric characters, dots, underscores, and hyphens");

        return new Username(value);
    }

    public bool Equals(Username? other) => other != null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Username);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
