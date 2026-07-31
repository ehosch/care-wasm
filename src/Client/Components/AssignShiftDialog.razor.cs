using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Care.Wasm.Client.Components;

public partial class AssignShiftDialog
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IShiftsClient ShiftsClient { get; set; } = default!;

    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Parameter]
    public Guid ShiftId { get; set; }

    [Parameter]
    public string ShiftLabel { get; set; } = string.Empty;

    [Parameter]
    public string? CurrentAssignedUserId { get; set; }

    [Parameter]
    public TimeSpan CurrentStartTime { get; set; }

    [Parameter]
    public TimeSpan CurrentEndTime { get; set; }

    private ICollection<UserDto> _activeUsers = new List<UserDto>();
    private string _selectedUserId = string.Empty;
    private int _startMinutes;
    private int _endMinutes;
    private bool _isAdmin;
    private bool _busy;
    private string? _errorMessage;
    private string? _conflictMessage;

    private string FormatMinutes(int minutes) => DateTime.Today.AddMinutes(minutes).ToString("h:mm tt");

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _isAdmin = authState.User.IsInRole("Admin");

        var users = await UsersClient.GetUsersAsync(null);
        _activeUsers = users.Where(u => u.Status == "Active").ToList();
        _selectedUserId = CurrentAssignedUserId ?? string.Empty;
        _startMinutes = (int)CurrentStartTime.TotalMinutes;
        _endMinutes = (int)CurrentEndTime.TotalMinutes;
    }

    private async Task SubmitAsync() => await SubmitAsync(confirmGap: false);

    private async Task SubmitAsync(bool confirmGap)
    {
        _busy = true;
        _errorMessage = null;
        _conflictMessage = null;
        try
        {
            if (_isAdmin)
            {
                string? userId = string.IsNullOrEmpty(_selectedUserId) ? null : _selectedUserId;
                await ShiftsClient.AssignAsync(ShiftId, null, new AssignShiftRequest { UserId = userId });
            }

            await ShiftsClient.AdjustTimesAsync(ShiftId, null, new AdjustShiftTimesRequest
            {
                StartTime = TimeSpan.FromMinutes(_startMinutes),
                EndTime = TimeSpan.FromMinutes(_endMinutes),
                ConfirmGap = confirmGap,
            });

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (ApiException ex) when (ex.StatusCode == 409 && !confirmGap)
        {
            _conflictMessage = ApiErrorHelper.GetFriendlyMessage(ex);
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

    private Task ConfirmGapAsync() => SubmitAsync(confirmGap: true);

    private void Cancel() => MudDialog.Cancel();
}
