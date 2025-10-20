namespace RSSVibe.Contracts.Auth;

/// <summary>
/// Request to refresh an expired JWT token using a refresh token.
/// </summary>
public sealed record RefreshTokenRequest(
    string RefreshToken
)
{
    public sealed class Validator : AbstractValidator<RefreshTokenRequest>
    {
        public Validator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required");
        }
    }
}
