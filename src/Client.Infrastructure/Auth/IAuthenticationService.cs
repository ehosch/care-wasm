using Care.Wasm.Client.Infrastructure.ApiClient;

namespace Care.Wasm.Client.Infrastructure.Auth;

public interface IAuthenticationService
{
    Task<bool> LoginAsync(TokenRequest request);

    Task LogoutAsync();
}
