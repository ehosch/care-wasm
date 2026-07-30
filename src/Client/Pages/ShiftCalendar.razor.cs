using System.Security.Claims;
using Care.Wasm.Client.Components;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Care.Wasm.Client.Pages;

public partial class ShiftCalendar
{
    private static readonly ShiftType[] _shiftTypes = { ShiftType.Day, ShiftType.Evening, ShiftType.Overnight };

    [Inject]
    private IShiftsClient ShiftsClient { get; set; } = default!;

    [Inject]
    private IReplacementRequestsClient ReplacementRequestsClient { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private DateOnly _weekStart;
    private ICollection<ShiftDto> _shifts = new List<ShiftDto>();
    private bool _loading = true;
    private string? _errorMessage;
    private string? _currentUserId;

    private string WeekRangeLabel =>
        $"{_weekStart:MMM d} – {_weekStart.AddDays(6):MMM d, yyyy}";

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _weekStart = StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        await LoadShiftsAsync();
    }

    private async Task LoadShiftsAsync()
    {
        _loading = true;
        try
        {
            _shifts = await ShiftsClient.GetShiftsAsync(
                new DateTimeOffset(_weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                null);
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

    private async Task PreviousWeekAsync()
    {
        _weekStart = _weekStart.AddDays(-7);
        await LoadShiftsAsync();
    }

    private async Task NextWeekAsync()
    {
        _weekStart = _weekStart.AddDays(7);
        await LoadShiftsAsync();
    }

    private async Task GoToThisWeekAsync()
    {
        _weekStart = StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        await LoadShiftsAsync();
    }

    private ShiftDto? FindShift(DateOnly date, ShiftType shiftType) =>
        _shifts.FirstOrDefault(s => DateOnly.FromDateTime(s.Date.Date) == date && s.ShiftType == shiftType);

    private async Task OpenAssignDialog(ShiftDto shift)
    {
        var parameters = new DialogParameters<AssignShiftDialog>
        {
            { d => d.ShiftId, shift.Id },
            { d => d.ShiftLabel, $"{shift.ShiftType} — {shift.Date:ddd, MMM d}" },
            { d => d.CurrentAssignedUserId, shift.AssignedUserId }
        };
        var result = await DialogService.ShowAsync<AssignShiftDialog>("Assign shift", parameters);
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadShiftsAsync();
        }
    }

    private async Task ClaimAsync(ShiftDto shift)
    {
        try
        {
            await ShiftsClient.ClaimAsync(shift.Id, null);
            await LoadShiftsAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task OpenRequestReplacementDialog(ShiftDto shift)
    {
        var parameters = new DialogParameters<RequestReplacementDialog>
        {
            { d => d.ShiftId, shift.Id },
            { d => d.ShiftLabel, $"{shift.ShiftType} — {shift.Date:ddd, MMM d}" }
        };
        var result = await DialogService.ShowAsync<RequestReplacementDialog>("Request replacement", parameters);
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadShiftsAsync();
        }
    }

    private async Task CancelReplacementRequestAsync(ShiftDto shift)
    {
        if (shift.PendingReplacementRequestId is not { } requestId)
        {
            return;
        }

        try
        {
            await ReplacementRequestsClient.CancelAsync(requestId, null);
            await LoadShiftsAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);
}
