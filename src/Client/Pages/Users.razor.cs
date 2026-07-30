using System.Security.Claims;
using Care.Wasm.Client.Components;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Care.Wasm.Client.Pages;

public partial class Users
{
    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private ICollection<UserDto> _users = new List<UserDto>();
    private bool _loading = true;
    private string? _errorMessage;
    private string? _currentUserId;

    private string? CurrentUserId => _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        _loading = true;
        try
        {
            _users = await UsersClient.GetUsersAsync(null);
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OpenInviteDialog()
    {
        var result = await DialogService.ShowAsync<InviteDialog>("Invite a member");
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadUsersAsync();
        }
    }

    private async Task ResendInviteAsync(UserDto user)
    {
        try
        {
            await UsersClient.ResendInviteAsync(user.Id, null);
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task RevokeInviteAsync(UserDto user)
    {
        try
        {
            await UsersClient.RevokeInviteAsync(user.Id, null);
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task ChangeRoleAsync(UserDto user, string role)
    {
        try
        {
            await UsersClient.ChangeRoleAsync(user.Id, null, new ChangeUserRoleRequest { Role = role });
            await LoadUsersAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
    }
}
