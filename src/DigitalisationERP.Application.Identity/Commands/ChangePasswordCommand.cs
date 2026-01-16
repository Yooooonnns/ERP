using DigitalisationERP.Application.DTOs.Identity.Requests;
using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

/// <summary>Command to change a user's password.</summary>
public class ChangePasswordCommand : IRequest<Result<string>>
{
    /// <summary>Gets the user ID.</summary>
    public long UserId { get; }

    /// <summary>Gets the old password.</summary>
    public string OldPassword { get; }

    /// <summary>Gets the new password.</summary>
    public string NewPassword { get; }

    /// <summary>Initializes a new instance of the ChangePasswordCommand class.</summary>
    public ChangePasswordCommand(long userId, string oldPassword, string newPassword)
    {
        UserId = userId;
        OldPassword = oldPassword;
        NewPassword = newPassword;
    }
}
