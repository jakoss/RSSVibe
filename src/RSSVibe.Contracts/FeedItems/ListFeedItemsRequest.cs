using RSSVibe.Contracts.FeedParseRuns;

namespace RSSVibe.Contracts.FeedItems;

/// <summary>
/// Query parameters for listing feed items. Filters FeedItem records per feed.
/// </summary>
public sealed record ListFeedItemsRequest(
    int Skip,
    int Take,
    string? Sort,
    DateTimeOffset? Since,
    DateTimeOffset? Until,
    ChangeKind? ChangeKind,
    bool IncludeMetadata
)
{

    public sealed class Validator : AbstractValidator<ListFeedItemsRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0).WithMessage("Skip must be >= 0");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 100).WithMessage("Take must be between 1 and 100");

            RuleFor(x => x.Sort)
                .Must(x => x is null ||
                    x.Equals("publishedAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("publishedAt:desc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("discoveredAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("discoveredAt:desc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("lastSeenAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("lastSeenAt:desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Sort must be one of: publishedAt, discoveredAt, lastSeenAt with :asc or :desc");

            RuleFor(x => x.ChangeKind)
                .IsInEnum()
                .WithMessage("ChangeKind must be one of: New, Refreshed, Unchanged");

            RuleFor(x => x.IncludeMetadata)
                .NotNull().WithMessage("IncludeMetadata must be specified");
        }
    }
}
