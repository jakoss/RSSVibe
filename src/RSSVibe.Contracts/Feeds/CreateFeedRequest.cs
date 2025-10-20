using RSSVibe.Contracts.FeedAnalyses;

namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to approve an analysis and create a feed. Creates Feed entity from FeedAnalysis.
/// </summary>
public sealed record CreateFeedRequest(
    Guid AnalysisId,
    string Title,
    string? Description,
    string? Language,
    UpdateIntervalDto UpdateInterval,
    short TtlMinutes,
    FeedSelectorsDto? SelectorsOverride
)
{

    public sealed class Validator : AbstractValidator<CreateFeedRequest>
    {
        public Validator()
        {
            RuleFor(x => x.AnalysisId)
                .NotEmpty().WithMessage("AnalysisId is required");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(200).WithMessage("Title must be at most 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Description must be at most 2000 characters");

            RuleFor(x => x.Language)
                .Must(x => x is null || (x.Length <= 16 && System.Text.RegularExpressions.Regex.IsMatch(x, "^[a-z]{2}(-[A-Z]{2})?$")))
                .WithMessage("Language must be a valid ISO 639-1/2 code");

            RuleFor(x => x.UpdateInterval)
                .NotNull().WithMessage("UpdateInterval is required");

            RuleFor(x => x.UpdateInterval.Unit)
                .IsInEnum()
                .WithMessage("UpdateInterval.Unit must be Hour, Day, or Week");

            RuleFor(x => x.UpdateInterval.Value)
                .GreaterThanOrEqualTo((short)1).WithMessage("UpdateInterval.Value must be >= 1");

            RuleFor(x => x.TtlMinutes)
                .GreaterThanOrEqualTo((short)15).WithMessage("TtlMinutes must be >= 15");

            // SelectorsOverride is optional but if provided must have required fields
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
