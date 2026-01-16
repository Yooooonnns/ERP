using DigitalisationERP.Domain.Identity.Enums;
using DigitalisationERP.Domain.Identity.ValueObjects;

namespace DigitalisationERP.Domain.Identity.Entities;

/// <summary>
/// User aggregate root following SAP standards with support for S-user, B-user, P-user, C-user types.
/// </summary>
public class User
{
    /// <summary>
    /// Gets the unique user identifier.
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    /// Gets the username value object.
    /// </summary>
    public Username Username { get; private set; } = null!;

    /// <summary>
    /// Gets the email value object.
    /// </summary>
    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Gets the password value object (hash + salt).
    /// </summary>
    public Password Password { get; private set; } = null!;

    /// <summary>
    /// Gets the user's first name.
    /// </summary>
    public string? FirstName { get; private set; }

    /// <summary>
    /// Gets the user's last name.
    /// </summary>
    public string? LastName { get; private set; }

    /// <summary>
    /// Gets the user type (S-user, B-user, P-user, or C-user).
    /// </summary>
    public UserTypeEnum UserType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user is locked.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Gets the date/time until which the user is locked (if applicable).
    /// </summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>
    /// Gets the date/time of the last successful login.
    /// </summary>
    public DateTime? LastLoginDate { get; private set; }

    /// <summary>
    /// Gets the date/time the password was last changed.
    /// </summary>
    public DateTime? PasswordChangedDate { get; private set; }

    /// <summary>
    /// Gets the date/time when the password expires.
    /// </summary>
    public DateTime? PasswordExpiryDate { get; private set; }

    /// <summary>
    /// Gets the date from which the user account is valid.
    /// </summary>
    public DateOnly? ValidFrom { get; private set; }

    /// <summary>
    /// Gets the date until which the user account is valid.
    /// </summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>
    /// Gets the number of failed login attempts.
    /// </summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>
    /// Gets the creation date/time.
    /// </summary>
    public DateTime CreatedDate { get; private set; }

    /// <summary>
    /// Gets the last modification date/time.
    /// </summary>
    public DateTime ModifiedDate { get; private set; }

    /// <summary>
    /// Gets the ID of the user who created this user.
    /// </summary>
    public long? CreatedBy { get; private set; }

    /// <summary>
    /// Gets the ID of the user who last modified this user.
    /// </summary>
    public long? ModifiedBy { get; private set; }

    /// <summary>
    /// Gets the collection of role assignments for this user.
    /// </summary>
    public ICollection<UserRoleAssignment> UserRoleAssignments { get; private set; } = new List<UserRoleAssignment>();

    /// <summary>
    /// Gets the collection of group assignments for this user.
    /// </summary>
    public ICollection<UserGroupAssignment> UserGroupAssignments { get; private set; } = new List<UserGroupAssignment>();

    /// <summary>
    /// Gets the collection of password history entries.
    /// </summary>
    public ICollection<PasswordHistory> PasswordHistoryEntries { get; private set; } = new List<PasswordHistory>();

    /// <summary>
    /// Gets the collection of session logs for this user.
    /// </summary>
    public ICollection<SessionLog> SessionLogs { get; private set; } = new List<SessionLog>();

    // Private constructor for EF Core
    private User() { }

    /// <summary>
    /// Creates a new User with the specified details.
    /// </summary>
    public static User Create(
        Username username,
        Email email,
        Password password,
        UserTypeEnum userType,
        string? firstName = null,
        string? lastName = null,
        DateOnly? validFrom = null,
        DateOnly? validTo = null,
        long? createdBy = null)
    {
        var user = new User
        {
            Username = username,
            Email = email,
            Password = password,
            UserType = userType,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            IsLocked = false,
            FailedLoginAttempts = 0,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            ValidFrom = validFrom,
            ValidTo = validTo,
            PasswordExpiryDate = DateTime.UtcNow.AddDays(90), // Default 90-day expiry
            CreatedBy = createdBy
        };

        return user;
    }

    /// <summary>
    /// Updates the user's password.
    /// </summary>
    public void UpdatePassword(Password newPassword)
    {
        Password = newPassword;
        PasswordChangedDate = DateTime.UtcNow;
        PasswordExpiryDate = DateTime.UtcNow.AddDays(90);
        FailedLoginAttempts = 0;
    }

    /// <summary>
    /// Locks the user account.
    /// </summary>
    public void Lock(DateTime? until = null)
    {
        IsLocked = true;
        LockedUntil = until ?? DateTime.UtcNow.AddMinutes(30); // Default 30 minutes
    }

    /// <summary>
    /// Unlocks the user account and resets failed login attempts.
    /// </summary>
    public void Unlock()
    {
        IsLocked = false;
        LockedUntil = null;
        FailedLoginAttempts = 0;
    }

    /// <summary>
    /// Records a successful login.
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginDate = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        
        // Auto-unlock if lock period has expired
        if (IsLocked && LockedUntil.HasValue && DateTime.UtcNow >= LockedUntil.Value)
        {
            Unlock();
        }
    }

    /// <summary>
    /// Records a failed login attempt.
    /// </summary>
    public void RecordFailedLoginAttempt(int maxAttempts = 5)
    {
        FailedLoginAttempts++;
        
        if (FailedLoginAttempts >= maxAttempts)
        {
            Lock();
        }
    }

    /// <summary>
    /// Checks if the user account is currently valid (active, not locked, within validity dates).
    /// </summary>
    public bool IsValidForLogin()
    {
        if (!IsActive || IsLocked)
            return false;

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        
        if (ValidFrom.HasValue && now < ValidFrom.Value)
            return false;

        if (ValidTo.HasValue && now > ValidTo.Value)
            return false;

        if (PasswordExpiryDate.HasValue && DateTime.UtcNow > PasswordExpiryDate.Value)
            return false;

        return true;
    }
}
