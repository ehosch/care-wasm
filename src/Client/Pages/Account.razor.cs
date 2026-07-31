using Care.Wasm.Client.Infrastructure.ApiClient;
using Care.Wasm.Client.Infrastructure.Auth;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Account
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Inject]
    private IAuthenticationService AuthService { get; set; } = default!;

    private bool _loading = true;
    private string? _currentEmail;

    private string _newEmail = string.Empty;
    private bool _emailBusy;
    private bool _emailSuccess;
    private string? _emailError;

    private string _phoneNumber = string.Empty;
    private bool _phoneBusy;
    private bool _phoneSaved;
    private string? _phoneError;

    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _passwordBusy;
    private string? _passwordError;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;
        try
        {
            var me = await UsersClient.GetMeAsync(null);
            _currentEmail = me.Email;
            _phoneNumber = me.PhoneNumber ?? string.Empty;
        }
        catch (ApiException ex)
        {
            _phoneError = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RequestEmailChangeAsync()
    {
        _emailBusy = true;
        _emailSuccess = false;
        _emailError = null;
        try
        {
            await UsersClient.RequestEmailChangeAsync(null, new RequestEmailChangeRequest { NewEmail = _newEmail });
            _emailSuccess = true;
        }
        catch (ApiException ex)
        {
            _emailError = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _emailBusy = false;
        }
    }

    private async Task SavePhoneAsync()
    {
        _phoneBusy = true;
        _phoneSaved = false;
        _phoneError = null;
        try
        {
            await UsersClient.UpdateMyPhoneNumberAsync(null, new UpdatePhoneNumberRequest
            {
                PhoneNumber = string.IsNullOrWhiteSpace(_phoneNumber) ? null : _phoneNumber,
            });
            _phoneSaved = true;
        }
        catch (ApiException ex)
        {
            _phoneError = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _phoneBusy = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        _passwordError = null;

        if (_newPassword != _confirmPassword)
        {
            _passwordError = "Passwords don't match.";
            return;
        }

        _passwordBusy = true;
        try
        {
            await UsersClient.ChangePasswordAsync(null, new ChangePasswordRequest
            {
                CurrentPassword = _currentPassword,
                NewPassword = _newPassword,
            });
            await AuthService.LogoutAsync();
        }
        catch (ApiException ex)
        {
            _passwordError = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _passwordBusy = false;
        }
    }
}
