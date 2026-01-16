using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

/// <summary>Command to update an existing user.</summary>
public class UpdateUserCommand : IRequest<Result<string>>
{
    /// <summary>Gets the user ID to update.</summary>
    public long UserId { get; }

    /// <summary>Gets the first name.</summary>
    public string FirstName { get; }

    /// <summary>Gets the last name.</summary>
    public string LastName { get; }

    /// <summary>Gets the email.</summary>
    public string Email { get; }

    /// <summary>Gets the phone number.</summary>
    public string? PhoneNumber { get; }

    /// <summary>Initializes a new instance of the UpdateUserCommand class.</summary>
    public UpdateUserCommand(long userId, string firstName, string lastName, string email, string? phoneNumber = null)
    {
        UserId = userId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
    }
}
