using FluentValidation;

namespace Momentum.Application.Users;

/// <summary>PATCH /me — any subset of fields; nulls mean "leave unchanged".</summary>
public sealed record UpdateMeRequest(
    string? Name,
    string? JobTitle,
    string? Timezone,
    string? Locale);

public sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    private static readonly string[] SupportedLocales = ["uz", "ru", "en"];

    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(200)
            .When(x => x.Name is not null);

        RuleFor(x => x.JobTitle)
            .MaximumLength(200)
            .When(x => x.JobTitle is not null);

        RuleFor(x => x.Locale)
            .Must(l => SupportedLocales.Contains(l))
            .WithMessage("Locale must be one of: uz, ru, en.")
            .When(x => x.Locale is not null);

        RuleFor(x => x.Timezone)
            .NotEmpty().MaximumLength(64)
            .Must(BeAValidTimezone).WithMessage("Unknown IANA timezone id.")
            .When(x => x.Timezone is not null);
    }

    private static bool BeAValidTimezone(string? tz)
        => tz is not null && TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _);
}
