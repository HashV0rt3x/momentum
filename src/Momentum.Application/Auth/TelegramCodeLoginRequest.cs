using FluentValidation;

namespace Momentum.Application.Auth;

/// <summary>POST /auth/telegram/code — exchange a bot-issued 6-digit code for tokens.</summary>
public sealed record TelegramCodeLoginRequest(string Code);

public sealed class TelegramCodeLoginRequestValidator : AbstractValidator<TelegramCodeLoginRequest>
{
    public TelegramCodeLoginRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("Code must be exactly 6 digits.");
    }
}
