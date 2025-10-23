namespace RSSVibe.Services.Auth;

/// <summary>
/// Enumeration of possible registration errors.
/// </summary>
public enum RegistrationError
{
    None,
    EmailAlreadyExists,
    InvalidPassword,
    IdentityStoreUnavailable
}
