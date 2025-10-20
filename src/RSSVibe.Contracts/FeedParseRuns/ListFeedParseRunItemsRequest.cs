namespace RSSVibe.Contracts.FeedParseRuns;

/// <summary>
/// Query parameters for listing items associated with a parse run. Filters FeedParseRunItem records.
/// </summary>
public sealed record ListFeedParseRunItemsRequest(
    ChangeKind? ChangeKind,
    int Skip,
    int Take
)
{

    public sealed class Validator : AbstractValidator<ListFeedParseRunItemsRequest>
    {
        public Validator()
        {
            RuleFor(x => x.ChangeKind)
                .IsInEnum()
                .WithMessage("ChangeKind must be one of: New, Refreshed, Unchanged");

            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0).WithMessage("Skip must be >= 0");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 100).WithMessage("Take must be between 1 and 100");
        }
    }
}
