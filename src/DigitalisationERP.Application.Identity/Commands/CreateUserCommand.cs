using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

/// <summary>Command to create a new user.</summary>
public class CreateUserCommand : IRequest<Result<RegisterResponse>>
{
    /// <summary>Gets the username.</summary>
    public string Username { get; }

    /// <summary>Gets the email.</summary>
    public string Email { get; }

    /// <summary>Gets the password.</summary>
    public string Password { get; }

    /// <summary>Gets the first name.</summary>
    public string FirstName { get; }

    /// <summary>Gets the last name.</summary>
    public string LastName { get; }

    /// <summary>Gets the phone number.</summary>
    public string? PhoneNumber { get; }

    /// <summary>Initializes a new instance of the CreateUserCommand class.</summary>
    public CreateUserCommand(string username, string email, string password, string firstName, string lastName, string? phoneNumber = null)
    {
        Username = username;
        Email = email;
        Password = password;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }
}
