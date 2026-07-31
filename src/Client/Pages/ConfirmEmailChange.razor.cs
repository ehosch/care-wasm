using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class ConfirmEmailChange
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "userId")]
    [Parameter]
    public string? UserId { get; set; }

    [SupplyParameterFromQuery(Name = "newEmail")]
    [Parameter]
    public string? NewEmail { get; set; }

    [SupplyParameterFromQuery(Name = "token")]
    [Parameter]
    public string? Token { get; set; }

    private string? _userId;
    private string? _newEmail;
    private string? _token;
    private bool _busy;
    private bool _success;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _userId = UserId;
        _newEmail = NewEmail;
        _token = Token;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;
        _busy = true;
        try
        {
            await UsersClient.ConfirmEmailChangeAsync(null, new ConfirmEmailChangeRequest
            {
                UserId = _userId!,
                NewEmail = _newEmail!,
                Token = _token!,
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
