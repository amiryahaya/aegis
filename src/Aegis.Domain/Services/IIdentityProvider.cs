using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Identity provider service for SSO/OIDC integration
/// Supports multiple identity providers (Azure AD, Okta, Auth0, Keycloak, etc.)
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Get the authorization URL for initiating OAuth2/OIDC flow
    /// </summary>
    Task<Result<AuthorizationUrl>> GetAuthorizationUrlAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    Task<Result<TokenResponse>> ExchangeCodeAsync(
        CodeExchangeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh an access token using a refresh token
    /// </summary>
    Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate an access token and get user info
    /// </summary>
    Task<Result<UserInfo>> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user info from the identity provider
    /// </summary>
    Task<Result<UserInfo>> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke a token (access or refresh)
    /// </summary>
    Task<Result<bool>> RevokeTokenAsync(
        string token,
        TokenType tokenType = TokenType.AccessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiate logout from the identity provider
    /// </summary>
    Task<Result<LogoutResponse>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the provider's OpenID Connect discovery document
    /// </summary>
    Task<Result<DiscoveryDocument>> GetDiscoveryDocumentAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available identity providers
    /// </summary>
    Task<Result<List<IdentityProviderInfo>>> GetProvidersAsync(
        CancellationToken cancellationToken = default);
}

#region Configuration

/// <summary>
/// Configuration for an identity provider
/// </summary>
public record IdentityProviderConfig
{
    public required string ProviderId { get; init; }
    public required string Name { get; init; }
    public required IdentityProviderType Type { get; init; }
    public required string Authority { get; init; }
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public required string RedirectUri { get; init; }
    public string? PostLogoutRedirectUri { get; init; }
    public List<string> Scopes { get; init; } = new() { "openid", "profile", "email" };
    public bool UsePkce { get; init; } = true;
    public bool ValidateIssuer { get; init; } = true;
    public bool ValidateAudience { get; init; } = true;
    public TimeSpan? TokenLifetime { get; init; }
    public Dictionary<string, string>? AdditionalParameters { get; init; }
    public ClaimMappings? ClaimMappings { get; init; }
}

/// <summary>
/// Supported identity provider types
/// </summary>
public enum IdentityProviderType
{
    Generic,
    AzureAD,
    AzureADB2C,
    Okta,
    Auth0,
    Keycloak,
    Google,
    GitHub,
    Custom
}

/// <summary>
/// Claim mappings from provider claims to internal claims
/// </summary>
public record ClaimMappings
{
    public string UserId { get; init; } = "sub";
    public string Email { get; init; } = "email";
    public string Name { get; init; } = "name";
    public string GivenName { get; init; } = "given_name";
    public string FamilyName { get; init; } = "family_name";
    public string Picture { get; init; } = "picture";
    public string Roles { get; init; } = "roles";
    public string Groups { get; init; } = "groups";
    public Dictionary<string, string>? CustomMappings { get; init; }
}

#endregion

#region Request/Response Types

/// <summary>
/// Request to initiate authorization
/// </summary>
public record AuthorizationRequest
{
    public string? ProviderId { get; init; }
    public string? State { get; init; }
    public string? Nonce { get; init; }
    public List<string>? AdditionalScopes { get; init; }
    public string? LoginHint { get; init; }
    public string? DomainHint { get; init; }
    public string? Prompt { get; init; } // none, login, consent, select_account
    public Dictionary<string, string>? AdditionalParameters { get; init; }
}

/// <summary>
/// Authorization URL response
/// </summary>
public record AuthorizationUrl
{
    public required string Url { get; init; }
    public required string State { get; init; }
    public string? Nonce { get; init; }
    public string? CodeVerifier { get; init; } // For PKCE
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Request to exchange authorization code for tokens
/// </summary>
public record CodeExchangeRequest
{
    public required string Code { get; init; }
    public required string State { get; init; }
    public string? CodeVerifier { get; init; } // For PKCE
    public string? ProviderId { get; init; }
}

/// <summary>
/// Token response from identity provider
/// </summary>
public record TokenResponse
{
    public required string AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? IdToken { get; init; }
    public required string TokenType { get; init; }
    public int ExpiresIn { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public List<string>? Scopes { get; init; }
    public UserInfo? UserInfo { get; init; }
}

/// <summary>
/// Token type for revocation
/// </summary>
public enum TokenType
{
    AccessToken,
    RefreshToken,
    IdToken
}

/// <summary>
/// User information from identity provider
/// </summary>
public record UserInfo
{
    public required string Subject { get; init; }
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? Name { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? Picture { get; init; }
    public string? Locale { get; init; }
    public List<string>? Roles { get; init; }
    public List<string>? Groups { get; init; }
    public Dictionary<string, object>? Claims { get; init; }
    public string? ProviderId { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Logout request
/// </summary>
public record LogoutRequest
{
    public string? IdTokenHint { get; init; }
    public string? State { get; init; }
    public string? ProviderId { get; init; }
}

/// <summary>
/// Logout response
/// </summary>
public record LogoutResponse
{
    public required string LogoutUrl { get; init; }
    public string? State { get; init; }
}

/// <summary>
/// OpenID Connect discovery document
/// </summary>
public record DiscoveryDocument
{
    public required string Issuer { get; init; }
    public required string AuthorizationEndpoint { get; init; }
    public required string TokenEndpoint { get; init; }
    public string? UserInfoEndpoint { get; init; }
    public string? JwksUri { get; init; }
    public string? EndSessionEndpoint { get; init; }
    public string? RevocationEndpoint { get; init; }
    public string? IntrospectionEndpoint { get; init; }
    public List<string>? ScopesSupported { get; init; }
    public List<string>? ResponseTypesSupported { get; init; }
    public List<string>? GrantTypesSupported { get; init; }
    public List<string>? ClaimsSupported { get; init; }
    public bool? CodeChallengeMethodsSupported { get; init; }
}

/// <summary>
/// Information about an available identity provider
/// </summary>
public record IdentityProviderInfo
{
    public required string ProviderId { get; init; }
    public required string Name { get; init; }
    public required IdentityProviderType Type { get; init; }
    public string? IconUrl { get; init; }
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
    public int DisplayOrder { get; init; }
}

#endregion

#region Error Definitions

public static class IdentityProviderErrors
{
    public static Error ProviderNotFound => Error.NotFound(
        "IdentityProvider.NotFound",
        "Identity provider not found");

    public static Error InvalidCode => Error.Validation(
        "IdentityProvider.InvalidCode",
        "Invalid authorization code");

    public static Error InvalidState => Error.Validation(
        "IdentityProvider.InvalidState",
        "Invalid or expired state parameter");

    public static Error TokenExpired => Error.Unauthorized(
        "IdentityProvider.TokenExpired",
        "Token has expired");

    public static Error InvalidToken => Error.Unauthorized(
        "IdentityProvider.InvalidToken",
        "Invalid token");

    public static Error TokenRevoked => Error.Unauthorized(
        "IdentityProvider.TokenRevoked",
        "Token has been revoked");

    public static Error ProviderError => Error.Internal(
        "IdentityProvider.ProviderError",
        "Error communicating with identity provider");

    public static Error ConfigurationError => Error.Internal(
        "IdentityProvider.ConfigurationError",
        "Identity provider configuration error");

    public static Error PkceRequired => Error.Validation(
        "IdentityProvider.PkceRequired",
        "PKCE code verifier is required");
}

#endregion
