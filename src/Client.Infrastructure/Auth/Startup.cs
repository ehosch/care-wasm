using Care.Wasm.Client.Infrastructure.Auth.Jwt;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Care.Wasm.Client.Infrastructure.Auth;

internal static class Startup
{
    public static IServiceCollection AddAuthentication(this IServiceCollection services) =>
        services
            .AddScoped<AuthenticationStateProvider, JwtAuthenticationService>()
            .AddScoped(sp => (IAuthenticationService)sp.GetRequiredService<AuthenticationStateProvider>())
            .AddScoped(sp => (IAccessTokenProvider)sp.GetRequiredService<AuthenticationStateProvider>())
            .AddScoped<IAccessTokenProviderAccessor, AccessTokenProviderAccessor>()
            .AddScoped<JwtAuthenticationHeaderHandler>();

    public static IHttpClientBuilder AddAuthenticationHandler(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<JwtAuthenticationHeaderHandler>();
}
