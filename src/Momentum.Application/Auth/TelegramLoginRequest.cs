using System.Text.Json;
using FluentValidation;

namespace Momentum.Application.Auth;

/// <summary>
/// POST /auth/telegram body. Exactly one of <see cref="Data"/> (source=widget)
/// or <see cref="InitData"/> (source=webapp) must be present. Data is kept as a
/// raw JsonElement because the Telegram signature covers every field Telegram
/// sent — including ones added after this code was written — so verification
/// must see the payload verbatim, not a lossy typed projection.
/// </summary>
public sealed record TelegramLoginRequest(
    string Source,
    JsonElement? Data,
    string? InitData);

public sealed class TelegramLoginRequestValidator : AbstractValidator<TelegramLoginRequest>
{
    public TelegramLoginRequestValidator()
    {
        RuleFor(x => x.Source)
            .Must(s => s is "widget" or "webapp")
            .WithMessage("source must be 'widget' or 'webapp'.");

        RuleFor(x => x.Data)
            .NotNull().WithMessage("data is required when source is 'widget'.")
            .When(x => x.Source == "widget");

        RuleFor(x => x.InitData)
            .NotEmpty().WithMessage("initData is required when source is 'webapp'.")
            .When(x => x.Source == "webapp");
    }
}
