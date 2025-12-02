namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Request to list feeds for the authenticated user.
/// </summary>
public sealed record ListFeedsRequest(
    int Skip = 0,
    int Take = 20,
    string Sort = "lastParsedAt:desc",
    string? Status = null,
    string? NextParseBefore = null,
    string? Search = null,
    bool IncludeInactive = false
)
{
    public sealed class Validator : AbstractValidator<ListFeedsRequest>
    {
        private static readonly string[] ValidSortFields = ["createdAt", "lastParsedAt", "title"];
        private static readonly string[] ValidSortDirections = ["asc", "desc"];
        private static readonly string[] ValidStatuses = ["scheduled", "running", "succeeded", "failed", "skipped"];

        public Validator()
        {
            RuleFor(x => x.Skip)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Skip must be greater than or equal to 0.");

            RuleFor(x => x.Take)
                .InclusiveBetween(1, 50)
                .WithMessage("Take must be between 1 and 50.");

            RuleFor(x => x.Sort)
                .NotEmpty()
                .WithMessage("Sort is required.")
                .Must(s =>
                {
                    var parts = s.Split(':');
                    if (parts.Length != 2) return false;
                    return ValidSortFields.Contains(parts[0]) && ValidSortDirections.Contains(parts[1]);
                })
                .WithMessage("Sort must be in format 'field:direction' where field is 'createdAt', 'lastParsedAt', or 'title', and direction is 'asc' or 'desc'.");

            RuleFor(x => x.Status)
                .Must(s => s == null || ValidStatuses.Contains(s))
                .WithMessage("Status must be one of: scheduled, running, succeeded, failed, skipped.");

            RuleFor(x => x.NextParseBefore)
                .Must(BeValidIso8601Timestamp!)
                .WithMessage("NextParseBefore must be a valid ISO 8601 timestamp.")
                .When(x => x.NextParseBefore is not null);
        }

        private static bool BeValidIso8601Timestamp(string timestamp)
        {
            return DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out _);
        }
    }
}
