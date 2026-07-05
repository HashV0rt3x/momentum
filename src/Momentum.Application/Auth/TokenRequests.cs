using FluentValidation;

namespace Momentum.Application.Auth;

/// <summary>POST /auth/refresh and POST /auth/logout body.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
