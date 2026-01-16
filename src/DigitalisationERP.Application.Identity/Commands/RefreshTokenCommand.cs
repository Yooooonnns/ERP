using DigitalisationERP.Core;
using MediatR;

namespace DigitalisationERP.Application.Identity.Commands;

public class RefreshTokenCommand : IRequest<Result<string>>
{
    public string RefreshToken { get; set; }
    public string AccessToken { get; set; }

    public RefreshTokenCommand(string refreshToken, string accessToken)
    {
        RefreshToken = refreshToken;
        AccessToken = accessToken;
    }
}
