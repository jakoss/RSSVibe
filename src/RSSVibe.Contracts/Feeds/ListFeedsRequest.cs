using RSSVibe.Contracts.FeedParseRuns;

namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Query parameters for listing feeds. Filters Feed records per user.
/// </summary>
public sealed record ListFeedsRequest(
    int Skip,
    int Take,
    string? Sort,
    FeedParseRunStatus? Status,
    DateTimeOffset? NextParseBefore,
    string? Search,
    bool IncludeInactive
)
{

    public sealed class Validator : AbstractValidator<ListFeedsRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0).WithMessage("Skip must be >= 0");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 50).WithMessage("Take must be between 1 and 50");

            RuleFor(x => x.Sort)
                .Must(x => x is null ||
                    x.Equals("createdAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("createdAt:desc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("lastParsedAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("lastParsedAt:desc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("title:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("title:desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Sort must be one of: createdAt, lastParsedAt, title with :asc or :desc");

            RuleFor(x => x.Status)
                .IsInEnum()
                .WithMessage("Status must be one of: Scheduled, Running, Succeeded, Failed, Skipped");

            RuleFor(x => x.Search)
                .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
                .WithMessage("Search must be either null or a non-empty string");

            RuleFor(x => x.IncludeInactive)
                .NotNull().WithMessage("IncludeInactive must be specified");
        }
    }
}
