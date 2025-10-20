namespace RSSVibe.Contracts.Feeds;

/// <summary>
/// Feed update interval configuration. Maps from Feed.UpdateIntervalUnit and Feed.UpdateIntervalValue.
/// </summary>
public sealed record UpdateIntervalDto(
    UpdateIntervalUnit Unit,
    short Value
);
