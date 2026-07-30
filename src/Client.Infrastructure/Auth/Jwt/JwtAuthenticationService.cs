using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Care.Wasm.Client.Infrastructure.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;

namespace Care.Wasm.Client.Infrastructure.Auth.Jwt;

public class JwtAuthenticationService : AuthenticationStateProvider, IAuthenticationService, IAccessTokenProvider
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly ILocalStorageService _localStorage;
    private readonly ITokensClient _tokensClient;
    private readonly NavigationManager _navigation;
    private readonly ILogger<JwtAuthenticationService> _logger;

    public JwtAuthenticationService(
        ILocalStorageService localStorage,
        ITokensClient tokensClient,
        NavigationManager navigation,
        ILogger<JwtAuthenticationService> logger)
    {
        _localStorage = localStorage;
        _tokensClient = tokensClient;
        _navigation = navigation;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? cachedToken = await GetCachedAuthTokenAsync();
        if (string.IsNullOrWhiteSpace(cachedToken))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claimsIdentity = new ClaimsIdentity(GetClaimsFromJwt(cachedToken), "jwt");
        return new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
    }

    public async Task<bool> LoginAsync(TokenRequest request)
    {
        var tokenResponse = await _tokensClient.GetTokenAsync(null, request);

        string? token = tokenResponse.Token;
        string? refreshToken = tokenResponse.RefreshToken;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        await CacheAuthTokens(token, refreshToken);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

        return true;
    }

    public async Task LogoutAsync()
    {
        await ClearCacheAsync();

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

        _navigation.NavigateTo("/login");
    }

    public async ValueTask<AccessTokenResult> RequestAccessToken()
    {
        var authState = await GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated is not true)
        {
            return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, null!, "/login", null);
        }

        await _semaphore.WaitAsync();
        try
        {
            string token = await GetCachedAuthTokenAsync() ?? string.Empty;

            var expClaim = authState.User.FindFirst("exp")?.Value;
            var expTime = expClaim is not null
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim)).UtcDateTime
                : DateTime.MinValue;
            var diff = expTime - DateTime.UtcNow;
            if (diff.TotalMinutes <= 1)
            {
                string refreshToken = await GetCachedRefreshTokenAsync() ?? string.Empty;
                (bool succeeded, var response) = await TryRefreshTokenAsync(new RefreshTokenRequest { Token = token, RefreshToken = refreshToken });
                if (!succeeded)
                {
                    _logger.LogWarning("Token refresh failed; redirecting to login. Token expiry was {Expiry:u}.", expTime);
                    return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, null!, "/login", null);
                }

                token = response?.Token ?? string.Empty;
            }

            return new AccessTokenResult(AccessTokenResultStatus.Success, new AccessToken { Value = token }, string.Empty, null);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options) =>
        RequestAccessToken();

    private async Task<(bool Succeeded, TokenResponse? Token)> TryRefreshTokenAsync(RefreshTokenRequest request)
    {
        try
        {
            var tokenResponse = await _tokensClient.RefreshAsync(null, request);

            await CacheAuthTokens(tokenResponse.Token, tokenResponse.RefreshToken);

            return (true, tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh API call failed. Exception: {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            return (false, null);
        }
    }

    private async ValueTask CacheAuthTokens(string? token, string? refreshToken)
    {
        await _localStorage.SetItemAsync(StorageConstants.Local.AuthToken, token);
        await _localStorage.SetItemAsync(StorageConstants.Local.RefreshToken, refreshToken);
    }

    private async Task ClearCacheAsync()
    {
        await _localStorage.RemoveItemAsync(StorageConstants.Local.AuthToken);
        await _localStorage.RemoveItemAsync(StorageConstants.Local.RefreshToken);
    }

    private ValueTask<string?> GetCachedAuthTokenAsync() =>
        _localStorage.GetItemAsync<string?>(StorageConstants.Local.AuthToken);

    private ValueTask<string?> GetCachedRefreshTokenAsync() =>
        _localStorage.GetItemAsync<string?>(StorageConstants.Local.RefreshToken);

    private static IEnumerable<Claim> GetClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        string payload = jwt.Split('.')[1];
        byte[] jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs is not null)
        {
            keyValuePairs.TryGetValue(ClaimTypes.Role, out object? roles);

            if (roles is not null)
            {
                string? rolesString = roles.ToString();
                if (!string.IsNullOrEmpty(rolesString))
                {
                    if (rolesString.Trim().StartsWith('['))
                    {
                        string[]? parsedRoles = JsonSerializer.Deserialize<string[]>(rolesString);
                        if (parsedRoles is not null)
                        {
                            claims.AddRange(parsedRoles.Select(role => new Claim(ClaimTypes.Role, role)));
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, rolesString));
                    }
                }

                keyValuePairs.Remove(ClaimTypes.Role);
            }

            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? string.Empty)));
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string payload)
    {
        payload = payload.Trim().Replace('-', '+').Replace('_', '/');
        string base64 = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
