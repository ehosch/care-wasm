using Care.Wasm.Client.Infrastructure.ApiClient;
using Care.Wasm.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Login
{
    [Inject]
    private IAuthenticationService AuthService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private readonly TokenRequest _tokenRequest = new();
    private bool _busySubmitting;
    private string? _errorMessage;

    private async Task SubmitAsync()
    {
        _busySubmitting = true;
        _errorMessage = null;
        try
        {
            bool succeeded = await AuthService.LoginAsync(_tokenRequest);
            if (succeeded)
            {
                Navigation.NavigateTo("/", forceLoad: false);
            }
            else
            {
                _errorMessage = "Invalid email or password.";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _busySubmitting = false;
        }
    }
}
