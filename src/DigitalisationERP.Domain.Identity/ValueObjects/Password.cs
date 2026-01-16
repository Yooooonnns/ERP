namespace DigitalisationERP.Domain.Identity.ValueObjects;

/// <summary>
/// Value object representing a password with its hash and salt.
/// </summary>
public class Password : IEquatable<Password>
{
    /// <summary>
    /// Gets the password hash.
    /// </summary>
    public string Hash { get; private set; }

    /// <summary>
    /// Gets the password salt.
    /// </summary>
    public string Salt { get; private set; }

    private Password(string hash, string salt)
    {
        Hash = hash;
        Salt = salt;
    }

    /// <summary>
    /// Creates a Password value object from a hash and salt.
    /// </summary>
    public static Password FromHashAndSalt(string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be empty", nameof(hash));

        if (string.IsNullOrWhiteSpace(salt))
            throw new ArgumentException("Salt cannot be empty", nameof(salt));

        return new Password(hash, salt);
    }

    public bool Equals(Password? other) => 
        other != null && Hash == other.Hash && Salt == other.Salt;

    public override bool Equals(object? obj) => Equals(obj as Password);
    
    public override int GetHashCode() => HashCode.Combine(Hash, Salt);
}
