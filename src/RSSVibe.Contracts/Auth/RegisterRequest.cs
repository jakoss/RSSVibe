namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request to register a new user account. Connected to ApplicationUser entity.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    bool MustChangePassword
)
{
    public sealed class Validator : AbstractValidator<RegisterRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email must be a valid email address");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required")
                .MinimumLength(12).WithMessage("Password must be at least 12 characters")
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches("[0-9]").WithMessage("Password must contain at least one digit")
                .Matches("[!@#$%^&*]").WithMessage("Password must contain at least one special character");

            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("Display name is required");

            RuleFor(x => x.MustChangePassword)
                .NotNull().WithMessage("MustChangePassword must be specified");
        }
    }
}
