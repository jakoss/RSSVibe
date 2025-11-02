namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request model for changing user password.
/// </summary>
/// <param name="CurrentPassword">User's current password for verification.</param>
/// <param name="NewPassword">New password meeting complexity requirements.</param>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword)
{
    /// <summary>
    /// Validator for password change requests.
    /// </summary>
    public sealed class Validator : AbstractValidator<ChangePasswordRequest>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty()
                .WithMessage("Current password is required.")
                .Length(12, 100)
                .WithMessage("Current password must be between 12 and 100 characters.");

            RuleFor(x => x.NewPassword)
                .NotEmpty()
                .WithMessage("New password is required.")
                .Length(12, 100)
                .WithMessage("New password must be between 12 and 100 characters.")
                .Matches(@"[A-Z]")
                .WithMessage("New password must contain at least one uppercase letter.")
                .Matches(@"[a-z]")
                .WithMessage("New password must contain at least one lowercase letter.")
                .Matches(@"[0-9]")
                .WithMessage("New password must contain at least one digit.")
                .Matches(@"[^a-zA-Z0-9]")
                .WithMessage("New password must contain at least one special character.");

            RuleFor(x => x)
                .Must(x => x.NewPassword != x.CurrentPassword)
                .WithMessage("New password must be different from current password.")
                .WithName("NewPassword");
        }
    }
}
