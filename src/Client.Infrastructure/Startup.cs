using Blazored.LocalStorage;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Care.Wasm.Client.Infrastructure.Auth;
using Care.Wasm.Client.Infrastructure.Common;
using Care.Wasm.Client.Infrastructure.Theme;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Care.Wasm.Client.Infrastructure;

public static class Startup
{
    private const string ClientName = "Care.API";

    public static IServiceCollection AddClientServices(this IServiceCollection services, IConfiguration config) =>
        services
            .AddBlazoredLocalStorage()
            .AddMudServices(configuration =>
            {
                configuration.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
                configuration.SnackbarConfiguration.HideTransitionDuration = 100;
                configuration.SnackbarConfiguration.ShowTransitionDuration = 100;
                configuration.SnackbarConfiguration.VisibleStateDuration = 3000;
                configuration.SnackbarConfiguration.ShowCloseIcon = false;
            })
            .AutoRegisterInterfaces<IApiService>()
            .AddAuthentication()
            .AddAuthorizationCore()
            .AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(config[ConfigNames.ApiBaseUrl]!);
            })
            .AddAuthenticationHandler()
            .Services
            .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName))
            .AddScoped<IThemeService, ThemeService>();

    private static IServiceCollection AutoRegisterInterfaces<T>(this IServiceCollection services)
    {
        var @interface = typeof(T);

        var types = @interface
            .Assembly
            .GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(t => new { Service = t.GetInterface($"I{t.Name}"), Implementation = t })
            .Where(t => t.Service is not null);

        foreach (var type in types)
        {
            if (@interface.IsAssignableFrom(type.Service))
            {
                services.AddTransient(type.Service!, type.Implementation);
            }
        }

        return services;
    }
}
