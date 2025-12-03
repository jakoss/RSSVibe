namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to create a feed from a completed analysis.
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
                .NotEmpty()
                .WithMessage("Analysis ID is required.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required.")
                .MaximumLength(200)
                .WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(2000)
                .WithMessage("Description must not exceed 2000 characters.")
                .When(x => x.Description is not null);

            RuleFor(x => x.Language)
                .MaximumLength(16)
                .WithMessage("Language code must not exceed 16 characters.")
                .Matches(@"^[a-z]{2,3}(-[A-Z]{2})?$")
                .WithMessage("Language must be a valid ISO 639-1/2 code (e.g., 'en', 'en-US').")
                .When(x => x.Language is not null);

            RuleFor(x => x.UpdateInterval)
                .NotNull()
                .WithMessage("Update interval is required.")
                .SetValidator(new UpdateIntervalDto.Validator());

            RuleFor(x => x.TtlMinutes)
                .GreaterThanOrEqualTo((short)15)
                .WithMessage("TTL must be at least 15 minutes.");

            RuleFor(x => x.SelectorsOverride)
                .SetValidator(new FeedSelectorsDto.Validator()!)
                .When(x => x.SelectorsOverride is not null);
        }
    }
}
