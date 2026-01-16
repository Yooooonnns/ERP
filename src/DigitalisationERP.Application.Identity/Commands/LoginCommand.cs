using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Application.DTOs.Identity.Responses;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

/// <summary>Command to log in a user and receive JWT tokens.</summary>
public class LoginCommand : IRequest<Result<LoginResponse>>
{
    /// <summary>Gets the username.</summary>
    public string Username { get; }

    /// <summary>Gets the password.</summary>
    public string Password { get; }

    /// <summary>Initializes a new instance of the LoginCommand class.</summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public LoginCommand(string username, string password)
    {
        Username = username;
        Password = password;
    }
}
