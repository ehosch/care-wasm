using System.Net.Http.Headers;
using Care.Wasm.Client.Infrastructure.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace Care.Wasm.Client.Infrastructure.Auth.Jwt;

public class JwtAuthenticationHeaderHandler : DelegatingHandler
{
    // Endpoints that are [AllowAnonymous] server-side. A missing/expired token here is
    // expected (the caller may not be logged in at all — e.g. forgot-password) and must
    // NOT force-navigate to /login; keep this list precise rather than a broad match —
    // matching too loosely previously broke authenticated calls on similarly-named routes.
    private static readonly string[] AnonymousPaths =
    {
        "/api/tokens",
        "/api/users/register",
        "/api/users/forgot-password",
        "/api/users/reset-password"
    };

    private readonly IAccessTokenProviderAccessor _tokenProviderAccessor;
    private readonly NavigationManager _navigation;

    public JwtAuthenticationHeaderHandler(IAccessTokenProviderAccessor tokenProviderAccessor, NavigationManager navigation)
    {
        _tokenProviderAccessor = tokenProviderAccessor;
        _navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool isAnonymousEndpoint = request.RequestUri is not null
            && AnonymousPaths.Any(p => request.RequestUri.AbsolutePath.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isAnonymousEndpoint)
        {
            if (await _tokenProviderAccessor.TokenProvider.GetAccessTokenAsync() is string token)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _navigation.NavigateTo("/login");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
