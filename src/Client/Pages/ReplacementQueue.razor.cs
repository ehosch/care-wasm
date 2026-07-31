using System.Security.Claims;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Care.Wasm.Client.Pages;

public partial class ReplacementQueue
{
    [Inject]
    private IReplacementRequestsClient ReplacementRequestsClient { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private ICollection<ReplacementRequestDto> _requests = new List<ReplacementRequestDto>();
    private bool _loading = true;
    private string? _errorMessage;
    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await LoadQueueAsync();
    }

    private async Task LoadQueueAsync()
    {
        _loading = true;
        try
        {
            _requests = await ReplacementRequestsClient.GetQueueAsync(null);
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ClaimAsync(ReplacementRequestDto request)
    {
        try
        {
            await ReplacementRequestsClient.ClaimAsync(request.Id, null);
            await LoadQueueAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
    }

    private async Task CancelAsync(ReplacementRequestDto request)
    {
        try
        {
            await ReplacementRequestsClient.CancelAsync(request.Id, null);
            await LoadQueueAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
    }
}
