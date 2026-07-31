using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Register
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "token")]
    [Parameter]
    public string? Token { get; set; }

    private readonly RegisterRequest _model = new();
    private string? _token;
    private string _confirmPassword = string.Empty;
    private bool _smsConsent;
    private bool _busy;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _token = Token;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;

        if (_model.Password != _confirmPassword)
        {
            _errorMessage = "Passwords don't match.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(_model.PhoneNumber) && !_smsConsent)
        {
            _errorMessage = "Please check the box to consent to SMS notifications, or leave the phone number blank.";
            return;
        }

        _busy = true;
        try
        {
            _model.Token = _token!;
            await UsersClient.RegisterAsync(null, _model);
            Navigation.NavigateTo("/login");
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
