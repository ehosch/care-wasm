using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Care.Wasm.Client.Components;

public partial class ReassignShiftDialog
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IShiftsClient ShiftsClient { get; set; } = default!;

    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Parameter]
    public Guid ShiftId { get; set; }

    [Parameter]
    public string ShiftLabel { get; set; } = string.Empty;

    [Parameter]
    public string CurrentAssignedUserId { get; set; } = string.Empty;

    private ICollection<UserDto> _activeUsers = new List<UserDto>();
    private string _selectedUserId = string.Empty;
    private bool _busy;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var users = await UsersClient.GetUsersAsync(null);
        _activeUsers = users.Where(u => u.Status == "Active").ToList();
        _selectedUserId = CurrentAssignedUserId;
    }

    private async Task SubmitAsync()
    {
        _busy = true;
        _errorMessage = null;
        try
        {
            await ShiftsClient.AssignAsync(ShiftId, null, new AssignShiftRequest { UserId = _selectedUserId });
            MudDialog.Close(DialogResult.Ok(true));
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

    private void Cancel() => MudDialog.Cancel();
}
