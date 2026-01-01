using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Identity;

/// <summary>
/// In-memory implementation of identity provider for development and testing
/// Simulates OAuth2/OIDC flows without connecting to an actual IdP
/// </summary>
public class InMemoryIdentityProvider : IIdentityProvider
{
    private readonly ILogger<InMemoryIdentityProvider> _logger;
    private readonly ConcurrentDictionary<string, AuthorizationState> _authorizationStates = new();
    private readonly ConcurrentDictionary<string, TokenInfo> _tokens = new();
    private readonly ConcurrentDictionary<string, UserInfo> _users = new();
    private readonly List<IdentityProviderConfig> _providers;

    public InMemoryIdentityProvider(
        ILogger<InMemoryIdentityProvider> logger,
        List<IdentityProviderConfig>? providers = null)
    {
        _logger = logger;
        _providers = providers ?? new List<IdentityProviderConfig>
        {
            new IdentityProviderConfig
            {
                ProviderId = "local",
                Name = "Local Development",
                Type = IdentityProviderType.Custom,
                Authority = "https://localhost",
                ClientId = "aegis-dev",
                RedirectUri = "https://localhost/callback",
                PostLogoutRedirectUri = "https://localhost"
            }
        };

        // Seed some test users
        SeedTestUsers();
    }

    public Task<Result<AuthorizationUrl>> GetAuthorizationUrlAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerId = request.ProviderId ?? _providers.FirstOrDefault()?.ProviderId;
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);

        if (provider == null)
        {
            return Task.FromResult(Result.Failure<AuthorizationUrl>(
                IdentityProviderErrors.ProviderNotFound));
        }

        var state = request.State ?? GenerateRandomString(32);
        var nonce = request.Nonce ?? GenerateRandomString(32);
        var codeVerifier = provider.UsePkce ? GenerateRandomString(64) : null;
        var codeChallenge = codeVerifier != null ? GenerateCodeChallenge(codeVerifier) : null;

        // Store authorization state
        _authorizationStates[state] = new AuthorizationState
        {
            State = state,
            Nonce = nonce,
            CodeVerifier = codeVerifier,
            ProviderId = providerId!,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        // Build authorization URL
        var scopes = provider.Scopes.ToList();
        if (request.AdditionalScopes != null)
            scopes.AddRange(request.AdditionalScopes);

        var urlBuilder = new StringBuilder();
        urlBuilder.Append($"{provider.Authority}/authorize?");
        urlBuilder.Append($"client_id={Uri.EscapeDataString(provider.ClientId)}");
        urlBuilder.Append($"&redirect_uri={Uri.EscapeDataString(provider.RedirectUri)}");
        urlBuilder.Append($"&response_type=code");
        urlBuilder.Append($"&scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        urlBuilder.Append($"&state={Uri.EscapeDataString(state)}");
        urlBuilder.Append($"&nonce={Uri.EscapeDataString(nonce)}");

        if (codeChallenge != null)
        {
            urlBuilder.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
            urlBuilder.Append("&code_challenge_method=S256");
        }

        if (!string.IsNullOrEmpty(request.LoginHint))
            urlBuilder.Append($"&login_hint={Uri.EscapeDataString(request.LoginHint)}");

        if (!string.IsNullOrEmpty(request.Prompt))
            urlBuilder.Append($"&prompt={Uri.EscapeDataString(request.Prompt)}");

        var result = new AuthorizationUrl
        {
            Url = urlBuilder.ToString(),
            State = state,
            Nonce = nonce,
            CodeVerifier = codeVerifier,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        _logger.LogInformation("Generated authorization URL for provider {ProviderId}", providerId);

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<TokenResponse>> ExchangeCodeAsync(
        CodeExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate state
        if (!_authorizationStates.TryRemove(request.State, out var authState))
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.InvalidState));
        }

        if (authState.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.InvalidState));
        }

        // Validate PKCE if required
        var provider = _providers.FirstOrDefault(p => p.ProviderId == authState.ProviderId);
        if (provider?.UsePkce == true)
        {
            if (string.IsNullOrEmpty(request.CodeVerifier))
            {
                return Task.FromResult(Result.Failure<TokenResponse>(
                    IdentityProviderErrors.PkceRequired));
            }

            var expectedChallenge = GenerateCodeChallenge(request.CodeVerifier);
            var storedChallenge = authState.CodeVerifier != null
                ? GenerateCodeChallenge(authState.CodeVerifier)
                : null;

            // In real implementation, we'd validate the code verifier
            // For testing, we accept if verifier was provided
        }

        // For testing, the code contains the user email
        var email = DecodeTestCode(request.Code);
        if (string.IsNullOrEmpty(email))
        {
            email = "test@example.com";
        }

        // Get or create user
        var userInfo = _users.Values.FirstOrDefault(u => u.Email == email) ?? CreateTestUser(email);

        // Generate tokens
        var accessToken = GenerateToken("access", userInfo.Subject);
        var refreshToken = GenerateToken("refresh", userInfo.Subject);
        var idToken = GenerateToken("id", userInfo.Subject);

        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        // Store token info
        _tokens[accessToken] = new TokenInfo
        {
            Token = accessToken,
            TokenType = TokenType.AccessToken,
            Subject = userInfo.Subject,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        _tokens[refreshToken] = new TokenInfo
        {
            Token = refreshToken,
            TokenType = TokenType.RefreshToken,
            Subject = userInfo.Subject,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            IsRevoked = false
        };

        var response = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IdToken = idToken,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            ExpiresAt = expiresAt,
            Scopes = provider?.Scopes,
            UserInfo = userInfo
        };

        _logger.LogInformation("Exchanged code for tokens for user {Subject}", userInfo.Subject);

        return Task.FromResult(Result.Success(response));
    }

    public Task<Result<TokenResponse>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryGetValue(refreshToken, out var tokenInfo))
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.InvalidToken));
        }

        if (tokenInfo.IsRevoked)
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.TokenRevoked));
        }

        if (tokenInfo.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.TokenExpired));
        }

        // Get user info
        var userInfo = _users.Values.FirstOrDefault(u => u.Subject == tokenInfo.Subject);
        if (userInfo == null)
        {
            return Task.FromResult(Result.Failure<TokenResponse>(
                IdentityProviderErrors.InvalidToken));
        }

        // Generate new access token
        var newAccessToken = GenerateToken("access", tokenInfo.Subject);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        _tokens[newAccessToken] = new TokenInfo
        {
            Token = newAccessToken,
            TokenType = TokenType.AccessToken,
            Subject = tokenInfo.Subject,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        var response = new TokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = refreshToken, // Same refresh token
            TokenType = "Bearer",
            ExpiresIn = 3600,
            ExpiresAt = expiresAt,
            UserInfo = userInfo
        };

        _logger.LogInformation("Refreshed token for user {Subject}", tokenInfo.Subject);

        return Task.FromResult(Result.Success(response));
    }

    public Task<Result<UserInfo>> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (!_tokens.TryGetValue(accessToken, out var tokenInfo))
        {
            return Task.FromResult(Result.Failure<UserInfo>(
                IdentityProviderErrors.InvalidToken));
        }

        if (tokenInfo.IsRevoked)
        {
            return Task.FromResult(Result.Failure<UserInfo>(
                IdentityProviderErrors.TokenRevoked));
        }

        if (tokenInfo.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Task.FromResult(Result.Failure<UserInfo>(
                IdentityProviderErrors.TokenExpired));
        }

        var userInfo = _users.Values.FirstOrDefault(u => u.Subject == tokenInfo.Subject);
        if (userInfo == null)
        {
            return Task.FromResult(Result.Failure<UserInfo>(
                IdentityProviderErrors.InvalidToken));
        }

        return Task.FromResult(Result.Success(userInfo));
    }

    public Task<Result<UserInfo>> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return ValidateTokenAsync(accessToken, cancellationToken);
    }

    public Task<Result<bool>> RevokeTokenAsync(
        string token,
        TokenType tokenType = TokenType.AccessToken,
        CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(token, out var tokenInfo))
        {
            tokenInfo = tokenInfo with { IsRevoked = true };
            _tokens[token] = tokenInfo;

            _logger.LogInformation("Revoked {TokenType} for user {Subject}",
                tokenType, tokenInfo.Subject);

            return Task.FromResult(Result.Success(true));
        }

        return Task.FromResult(Result.Success(false));
    }

    public Task<Result<LogoutResponse>> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerId = request.ProviderId ?? _providers.FirstOrDefault()?.ProviderId;
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);

        var logoutUrl = provider?.PostLogoutRedirectUri ?? "https://localhost";

        if (!string.IsNullOrEmpty(request.State))
        {
            logoutUrl += $"?state={Uri.EscapeDataString(request.State)}";
        }

        var response = new LogoutResponse
        {
            LogoutUrl = logoutUrl,
            State = request.State
        };

        _logger.LogInformation("Initiated logout for provider {ProviderId}", providerId);

        return Task.FromResult(Result.Success(response));
    }

    public Task<Result<DiscoveryDocument>> GetDiscoveryDocumentAsync(
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault();
        if (provider == null)
        {
            return Task.FromResult(Result.Failure<DiscoveryDocument>(
                IdentityProviderErrors.ProviderNotFound));
        }

        var discovery = new DiscoveryDocument
        {
            Issuer = provider.Authority,
            AuthorizationEndpoint = $"{provider.Authority}/authorize",
            TokenEndpoint = $"{provider.Authority}/token",
            UserInfoEndpoint = $"{provider.Authority}/userinfo",
            JwksUri = $"{provider.Authority}/.well-known/jwks.json",
            EndSessionEndpoint = $"{provider.Authority}/logout",
            RevocationEndpoint = $"{provider.Authority}/revoke",
            ScopesSupported = provider.Scopes,
            ResponseTypesSupported = new List<string> { "code", "token", "id_token" },
            GrantTypesSupported = new List<string> { "authorization_code", "refresh_token" },
            ClaimsSupported = new List<string> { "sub", "email", "name", "picture", "roles" },
            CodeChallengeMethodsSupported = true
        };

        return Task.FromResult(Result.Success(discovery));
    }

    public Task<Result<List<IdentityProviderInfo>>> GetProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = _providers.Select((p, i) => new IdentityProviderInfo
        {
            ProviderId = p.ProviderId,
            Name = p.Name,
            Type = p.Type,
            IsEnabled = true,
            DisplayOrder = i
        }).ToList();

        return Task.FromResult(Result.Success(providers));
    }

    #region Helper Methods

    private void SeedTestUsers()
    {
        var users = new[]
        {
            new UserInfo
            {
                Subject = "user-001",
                Email = "admin@aegis.local",
                EmailVerified = true,
                Name = "Admin User",
                GivenName = "Admin",
                FamilyName = "User",
                Roles = new List<string> { "Admin", "User" },
                ProviderId = "local"
            },
            new UserInfo
            {
                Subject = "user-002",
                Email = "user@aegis.local",
                EmailVerified = true,
                Name = "Test User",
                GivenName = "Test",
                FamilyName = "User",
                Roles = new List<string> { "User" },
                ProviderId = "local"
            },
            new UserInfo
            {
                Subject = "user-003",
                Email = "test@example.com",
                EmailVerified = true,
                Name = "Example User",
                GivenName = "Example",
                FamilyName = "User",
                Roles = new List<string> { "User" },
                ProviderId = "local"
            }
        };

        foreach (var user in users)
        {
            _users[user.Subject] = user;
        }
    }

    private UserInfo CreateTestUser(string email)
    {
        var subject = $"user-{Guid.NewGuid():N}"[..12];
        var user = new UserInfo
        {
            Subject = subject,
            Email = email,
            EmailVerified = true,
            Name = email.Split('@')[0],
            Roles = new List<string> { "User" },
            ProviderId = "local"
        };

        _users[subject] = user;
        return user;
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var data = new byte[length];
        RandomNumberGenerator.Fill(data);
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[data[i] % chars.Length];
        }
        return new string(result);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateToken(string type, string subject)
    {
        return $"{type}_{subject}_{GenerateRandomString(32)}";
    }

    private static string? DecodeTestCode(string code)
    {
        // For testing, codes can be formatted as "code_email@example.com"
        if (code.StartsWith("code_") && code.Contains('@'))
        {
            return code[5..];
        }
        return null;
    }

    #endregion

    #region Internal Types

    private record AuthorizationState
    {
        public required string State { get; init; }
        public required string Nonce { get; init; }
        public string? CodeVerifier { get; init; }
        public required string ProviderId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private record TokenInfo
    {
        public required string Token { get; init; }
        public required TokenType TokenType { get; init; }
        public required string Subject { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public bool IsRevoked { get; init; }
    }

    #endregion
}
