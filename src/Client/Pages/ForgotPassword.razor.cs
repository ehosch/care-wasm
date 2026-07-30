using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class ForgotPassword
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    private string _email = string.Empty;
    private bool _busy;
    private bool _submitted;

    private async Task SubmitAsync()
    {
        _busy = true;
        try
        {
            await UsersClient.ForgotPasswordAsync(null, new ForgotPasswordRequest { Email = _email });
        }
        catch (ApiException)
        {
            // Intentionally swallow — always show the generic success message so we don't
            // reveal whether an account exists for this email.
        }
        finally
        {
            _busy = false;
            _submitted = true;
        }
    }
}
