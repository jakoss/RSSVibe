namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Request to initiate AI-powered analysis of a feed URL. Maps to FeedAnalysis entity.
/// </summary>
public sealed record CreateFeedAnalysisRequest(
    string TargetUrl,
    string? AiModel,
    bool ForceReanalysis
)
{
    public sealed class Validator : AbstractValidator<CreateFeedAnalysisRequest>
    {
        public Validator()
        {
            RuleFor(x => x.TargetUrl)
                .NotEmpty().WithMessage("Target URL is required")
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var result) && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                .WithMessage("Target URL must be a valid absolute HTTPS URL");

            RuleFor(x => x.AiModel)
                .Must(x => x is null || !string.IsNullOrWhiteSpace(x))
                .WithMessage("AI model must be either null or a non-empty string");

            RuleFor(x => x.ForceReanalysis)
                .NotNull().WithMessage("ForceReanalysis must be specified");
        }
    }
}
