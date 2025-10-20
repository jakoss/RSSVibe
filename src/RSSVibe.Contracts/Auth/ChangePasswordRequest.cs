namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request to change user password. Updates ApplicationUser password hash.
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
)
{
    public sealed class Validator : AbstractValidator<ChangePasswordRequest>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(12).WithMessage("New password must be at least 12 characters")
                .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter")
                .Matches("[a-z]").WithMessage("New password must contain at least one lowercase letter")
                .Matches("[0-9]").WithMessage("New password must contain at least one digit")
                .Matches("[!@#$%^&*]").WithMessage("New password must contain at least one special character")
                .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password");
        }
    }
}
