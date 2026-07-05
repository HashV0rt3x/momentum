using FluentValidation;
using Momentum.Domain.Focus;

namespace Momentum.Application.Focus;

/// <summary>Spec section 8.</summary>
public sealed record FocusSessionDto(
    string Id,
    string Type,
    string? TaskId,
    int PlannedMinutes,
    int CompletedMinutes,
    bool Completed,
    DateTimeOffset StartedAt);

public sealed record CreateFocusSessionRequest(
    string Type,
    Guid? TaskId,
    int PlannedMinutes,
    int CompletedMinutes,
    bool Completed,
    DateTimeOffset StartedAt);

/// <summary>
/// GET /focus-sessions query: either date=today (resolved in the user's
/// timezone) or an explicit from/to ISO range. No params = today.
/// </summary>
public sealed record FocusSessionListQuery(string? Date, DateTimeOffset? From, DateTimeOffset? To);

public static class FocusEnumMapping
{
    public static string ToWire(this FocusSessionType type) => type switch
    {
        FocusSessionType.Focus => "focus",
        FocusSessionType.ShortBreak => "short_break",
        FocusSessionType.LongBreak => "long_break",
        _ => "focus",
    };

    public static bool TryParse(string value, out FocusSessionType type)
    {
        type = value switch
        {
            "focus" => FocusSessionType.Focus,
            "short_break" => FocusSessionType.ShortBreak,
            "long_break" => FocusSessionType.LongBreak,
            _ => (FocusSessionType)(-1),
        };
        return (int)type >= 0;
    }
}

public sealed class CreateFocusSessionRequestValidator : AbstractValidator<CreateFocusSessionRequest>
{
    public CreateFocusSessionRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => FocusEnumMapping.TryParse(t, out _))
            .WithMessage("Type must be one of: focus, short_break, long_break.");

        RuleFor(x => x.PlannedMinutes).InclusiveBetween(1, 480);
        RuleFor(x => x.CompletedMinutes).InclusiveBetween(0, 480);
        RuleFor(x => x.CompletedMinutes)
            .LessThanOrEqualTo(x => x.PlannedMinutes)
            .WithMessage("completedMinutes cannot exceed plannedMinutes.");
    }
}

public interface IFocusSessionService
{
    Task<IReadOnlyList<FocusSessionDto>> ListAsync(Guid userId, FocusSessionListQuery query, CancellationToken cancellationToken);

    Task<FocusSessionDto> CreateAsync(Guid userId, CreateFocusSessionRequest request, CancellationToken cancellationToken);
}
