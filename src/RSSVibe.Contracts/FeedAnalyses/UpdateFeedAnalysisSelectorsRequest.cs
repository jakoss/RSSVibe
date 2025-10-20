namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Request to update CSS selectors for a feed analysis. Persists to FeedAnalysis.Selectors.
/// </summary>
public sealed record UpdateFeedAnalysisSelectorsRequest(
    FeedSelectorsDto Selectors
)
{
    public sealed class Validator : AbstractValidator<UpdateFeedAnalysisSelectorsRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Selectors).NotNull().WithMessage("Selectors must be provided");

            RuleFor(x => x.Selectors.ItemContainer)
                .NotEmpty().WithMessage("ItemContainer selector is required");

            RuleFor(x => x.Selectors.Title)
                .NotEmpty().WithMessage("Title selector is required");

            RuleFor(x => x.Selectors.Link)
                .NotEmpty().WithMessage("Link selector is required");
        }
    }
}
