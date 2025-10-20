namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Query parameters for listing feed parse runs. Filters FeedParseRun records per feed.
/// </summary>
public sealed record ListFeedParseRunsRequest(
    int Skip,
    int Take,
    FeedParseRunStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    bool IncludeFailuresOnly,
    bool IncludeResponseHeaders
)
{

    public sealed class Validator : AbstractValidator<ListFeedParseRunsRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0).WithMessage("Skip must be >= 0");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 50).WithMessage("Take must be between 1 and 50");

            RuleFor(x => x.Status)
                .IsInEnum()
                .WithMessage("Status must be one of: Scheduled, Running, Succeeded, Failed, Skipped");

            RuleFor(x => x.IncludeFailuresOnly)
                .NotNull().WithMessage("IncludeFailuresOnly must be specified");

            RuleFor(x => x.IncludeResponseHeaders)
                .NotNull().WithMessage("IncludeResponseHeaders must be specified");
        }
    }
}
