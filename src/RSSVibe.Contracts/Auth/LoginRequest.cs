namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request to authenticate user credentials. Maps to ApplicationUser authentication.
/// </summary>
public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe,
    bool UseCookieAuth = true
)
{
    public sealed class Validator : AbstractValidator<LoginRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Email must be a valid email address");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required");

            RuleFor(x => x.RememberMe)
                .NotNull().WithMessage("RememberMe must be specified");

            RuleFor(x => x.UseCookieAuth)
                .NotNull().WithMessage("UseCookieAuth must be specified");
        }
    }
}
