namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Query parameters for listing feed analyses. Filters FeedAnalysis records per user.
/// </summary>
public sealed record ListFeedAnalysesRequest(
    FeedAnalysisStatus? Status,
    string? Sort,
    int Skip,
    int Take,
    string? Search
)
{

    public sealed class Validator : AbstractValidator<ListFeedAnalysesRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Status)
                .IsInEnum()
                .WithMessage("Status must be one of: Pending, InProgress, Completed, Failed, Superseded");

            RuleFor(x => x.Sort)
                .Must(x => x is null ||
                    x.Equals("createdAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("createdAt:desc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("updatedAt:asc", StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("updatedAt:desc", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Sort must be one of: createdAt:asc, createdAt:desc, updatedAt:asc, updatedAt:desc");

            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0).WithMessage("Skip must be >= 0");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 50).WithMessage("Take must be between 1 and 50");

            RuleFor(x => x.Search)
                .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
                .WithMessage("Search must be either null or a non-empty string");
        }
    }
}
