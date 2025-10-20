using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to update mutable feed fields. Updates Feed entity.
/// </summary>
public sealed record UpdateFeedRequest(
    string? Title,
    string? Description,
    string? Language,
    UpdateIntervalDto? UpdateInterval,
    short? TtlMinutes,
    FeedSelectorsDto? SelectorsOverride
)
{

    public sealed class Validator : AbstractValidator<UpdateFeedRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Title)
                .MaximumLength(200).WithMessage("Title must be at most 200 characters")
                .When(x => x.Title is not null);

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must be at most 2000 characters")
                .When(x => x.Description is not null);

            RuleFor(x => x.Language)
                .Must(x => x is not null && System.Text.RegularExpressions.Regex.IsMatch(x, "^[a-z]{2}(-[A-Z]{2})?$"))
                .WithMessage("Language must be a valid ISO 639-1/2 code")
                .When(x => x.Language is not null);

            RuleFor(x => x.UpdateInterval!.Unit)
                .IsInEnum()
                .WithMessage("UpdateInterval.Unit must be Hour, Day, or Week")
                .When(x => x.UpdateInterval is not null);

            RuleFor(x => x.UpdateInterval!.Value)
                .GreaterThanOrEqualTo((short)1).WithMessage("UpdateInterval.Value must be >= 1")
                .When(x => x.UpdateInterval is not null);

            RuleFor(x => x.TtlMinutes)
                .GreaterThanOrEqualTo((short)15).WithMessage("TtlMinutes must be >= 15")
                .When(x => x.TtlMinutes.HasValue);

            RuleFor(x => x.SelectorsOverride!.ItemContainer)
                .NotEmpty().WithMessage("SelectorsOverride.ItemContainer is required if override is provided")
                .When(x => x.SelectorsOverride is not null);

            RuleFor(x => x.SelectorsOverride!.Title)
                .NotEmpty().WithMessage("SelectorsOverride.Title is required if override is provided")
                .When(x => x.SelectorsOverride is not null);

            RuleFor(x => x.SelectorsOverride!.Link)
                .NotEmpty().WithMessage("SelectorsOverride.Link is required if override is provided")
                .When(x => x.SelectorsOverride is not null);
        }
    }
}
