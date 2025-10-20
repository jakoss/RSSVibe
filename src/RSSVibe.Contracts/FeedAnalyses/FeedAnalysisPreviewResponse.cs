namespace RSSVibe.Contracts.FeedAnalyses;

/// <summary>
/// Preview response for feed analysis showing up to 10 items extracted using current selectors.
/// </summary>
public sealed record FeedAnalysisPreviewResponse(
    Guid AnalysisId,
    FeedAnalysisPreviewItemDto[] Items
);
