namespace RSSVibe.Data.Abstractions;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; init; }
    DateTimeOffset UpdatedAt { get; set; }
}
