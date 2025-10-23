using Microsoft.AspNetCore.Identity;

namespace RSSVibe.Data.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// User's display name for UI purposes.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Flag indicating if user must change password on first login.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
