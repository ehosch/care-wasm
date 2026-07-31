using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class ResetPassword
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "token")]
    [Parameter]
    public string? Token { get; set; }

    [SupplyParameterFromQuery(Name = "email")]
    [Parameter]
    public string? Email { get; set; }

    private string? _token;
    private string? _email;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _busy;
    private bool _success;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _token = Token;
        _email = Email;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;

        if (_newPassword != _confirmPassword)
        {
            _errorMessage = "Passwords don't match.";
            return;
        }

        _busy = true;
        try
        {
            await UsersClient.ResetPasswordAsync(null, new ResetPasswordRequest
            {
                Email = _email!,
                Token = _token!,
                NewPassword = _newPassword
            });
            _success = true;
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _busy = false;
        }
    }
}
