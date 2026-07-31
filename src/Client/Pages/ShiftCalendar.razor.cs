using System.Security.Claims;
using Care.Wasm.Client.Components;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace Care.Wasm.Client.Pages;

public partial class ShiftCalendar
{
    private enum CellKind
    {
        Uncovered,
        Shift,
        Pending
    }

    private sealed record CellInfo(CellKind Kind, ShiftDto? Shift, bool IsFirstHour, bool IsLastHour);

    [Inject]
    private IShiftsClient ShiftsClient { get; set; } = default!;

    [Inject]
    private IReplacementRequestsClient ReplacementRequestsClient { get; set; } = default!;

    [Inject]
    private IUsersClient UsersClient { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private DateOnly _weekStart;
    private List<ShiftDto> _shifts = new();
    private ICollection<UserDto> _activeUsers = new List<UserDto>();
    private bool _loading = true;
    private string? _errorMessage;
    private string? _currentUserId;
    private bool _isAdmin;

    private bool _isCreating;
    private Guid? _editingShiftId;
    private DateTime? _pendingStart;
    private DateTime? _pendingEnd;
    private string _createAssignedUserId = string.Empty;
    private bool _busy;

    private string WeekRangeLabel =>
        $"{_weekStart:MMM d} – {_weekStart.AddDays(6):MMM d, yyyy}";

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _isAdmin = authState.User.IsInRole("Admin");
        _weekStart = StartOfWeek(DateOnly.FromDateTime(DateTime.Today));

        if (_isAdmin)
        {
            try
            {
                var users = await UsersClient.GetUsersAsync(null);
                _activeUsers = users.Where(u => u.Status == "Active").ToList();
            }
            catch (ApiException)
            {
                // Non-fatal — the assignee picker just won't have other options.
            }
        }

        await LoadShiftsAsync();
    }

    private async Task LoadShiftsAsync()
    {
        _loading = true;
        try
        {
            var result = await ShiftsClient.GetShiftsAsync(
                new DateTimeOffset(_weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                null);
            _shifts = result.ToList();
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

    private async Task PreviousWeekAsync()
    {
        ClearPending();
        _weekStart = _weekStart.AddDays(-7);
        await LoadShiftsAsync();
    }

    private async Task NextWeekAsync()
    {
        ClearPending();
        _weekStart = _weekStart.AddDays(7);
        await LoadShiftsAsync();
    }

    private async Task GoToThisWeekAsync()
    {
        ClearPending();
        _weekStart = StartOfWeek(DateOnly.FromDateTime(DateTime.Today));
        await LoadShiftsAsync();
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);

    private static DateTime GetAbsoluteStart(ShiftDto shift) =>
        DateOnly.FromDateTime(shift.Date.Date).ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime));

    private static DateTime GetAbsoluteEnd(ShiftDto shift)
    {
        var start = GetAbsoluteStart(shift);
        var end = DateOnly.FromDateTime(shift.Date.Date).ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime));
        return end <= start ? end.AddDays(1) : end;
    }

    private CellInfo GetCellInfo(DateOnly day, int hour)
    {
        var cellStart = day.ToDateTime(TimeOnly.MinValue).AddHours(hour);
        var cellEnd = cellStart.AddHours(1);

        if (_pendingStart is { } ps && _pendingEnd is { } pe && ps < cellEnd && pe > cellStart)
        {
            return new CellInfo(CellKind.Pending, null, cellStart <= ps, cellEnd >= pe);
        }

        var shift = _shifts.FirstOrDefault(s => GetAbsoluteStart(s) < cellEnd && GetAbsoluteEnd(s) > cellStart);
        if (shift is null)
        {
            return new CellInfo(CellKind.Uncovered, null, true, true);
        }

        return new CellInfo(CellKind.Shift, shift, GetAbsoluteStart(shift) >= cellStart, GetAbsoluteEnd(shift) <= cellEnd);
    }

    private bool CanEdit(ShiftDto shift) => _isAdmin || shift.AssignedUserId == _currentUserId;

    private string CellStyle(CellInfo info)
    {
        string background = info.Kind switch
        {
            CellKind.Pending => "#bbdefb",
            CellKind.Uncovered => "#fafafa",
            CellKind.Shift when info.Shift!.Status == ShiftStatus.ReplacementRequested => "#fff8e1",
            CellKind.Shift => "#e8f5e9",
            _ => "transparent",
        };

        bool clickable = info.Kind is CellKind.Uncovered or CellKind.Pending
            || (info.Kind == CellKind.Shift && CanEdit(info.Shift!));

        string borderTop = info.IsFirstHour ? "1px solid #9e9e9e" : "1px solid #eeeeee";
        string borderBottom = info.IsLastHour ? "1px solid #9e9e9e" : "none";

        return $"background-color:{background};border-left:1px solid #dddddd;border-right:1px solid #dddddd;" +
               $"border-top:{borderTop};border-bottom:{borderBottom};height:22px;vertical-align:top;" +
               $"cursor:{(clickable ? "pointer" : "default")};";
    }

    private static string FormatHourLabel(int hour) => DateTime.Today.AddHours(hour).ToString("h tt");

    private static string FormatTime(TimeSpan time) => DateTime.Today.Add(time).ToString("h:mm tt");

    private static string FormatTimeRange(ShiftDto shift) => $"{FormatTime(shift.StartTime)}–{FormatTime(shift.EndTime)}";

    private static string FormatRange(DateTime start, DateTime end) =>
        start.Date == end.Date
            ? $"{start:ddd, MMM d} {start:h:mm tt}–{end:h:mm tt}"
            : $"{start:ddd, MMM d h:mm tt} – {end:ddd, MMM d h:mm tt}";

    private void HandleCellClick(DateOnly day, int hour)
    {
        var cellStart = day.ToDateTime(TimeOnly.MinValue).AddHours(hour);
        var cellEnd = cellStart.AddHours(1);

        if (_pendingStart is null || _pendingEnd is null)
        {
            var info = GetCellInfo(day, hour);
            if (info.Kind == CellKind.Uncovered)
            {
                StartCreating(cellStart, cellEnd);
            }
            else if (info.Kind == CellKind.Shift && info.Shift is not null && CanEdit(info.Shift))
            {
                StartEditing(info.Shift);
            }

            return;
        }

        var ps = _pendingStart.Value;
        var pe = _pendingEnd.Value;

        if (cellStart == ps)
        {
            var newStart = cellEnd;
            if (newStart >= pe)
            {
                ClearPending();
            }
            else
            {
                _pendingStart = newStart;
            }
        }
        else if (cellEnd == pe)
        {
            var newEnd = cellStart;
            if (newEnd <= ps)
            {
                ClearPending();
            }
            else
            {
                _pendingEnd = newEnd;
            }
        }
        else if (cellEnd == ps)
        {
            _pendingStart = cellStart;
        }
        else if (cellStart == pe)
        {
            _pendingEnd = cellEnd;
        }
    }

    private void StartCreating(DateTime start, DateTime end)
    {
        _isCreating = true;
        _editingShiftId = null;
        _pendingStart = start;
        _pendingEnd = end;
        _createAssignedUserId = _currentUserId ?? string.Empty;
        _errorMessage = null;
    }

    private void StartEditing(ShiftDto shift)
    {
        _isCreating = false;
        _editingShiftId = shift.Id;
        _pendingStart = GetAbsoluteStart(shift);
        _pendingEnd = GetAbsoluteEnd(shift);
        _errorMessage = null;
    }

    private void ClearPending()
    {
        _isCreating = false;
        _editingShiftId = null;
        _pendingStart = null;
        _pendingEnd = null;
        _errorMessage = null;
    }

    private async Task SaveAsync()
    {
        if (_pendingStart is not { } start || _pendingEnd is not { } end)
        {
            return;
        }

        var swallowed = _shifts.FirstOrDefault(s =>
            s.Id != _editingShiftId && GetAbsoluteStart(s) >= start && GetAbsoluteEnd(s) <= end);
        if (swallowed is not null)
        {
            bool? confirmed = await DialogService.ShowMessageBox(
                "Remove a shift?",
                $"This will remove {swallowed.AssignedUserName}'s shift on {swallowed.Date:ddd, MMM d} ({FormatTimeRange(swallowed)}). Continue?",
                yesText: "Continue", cancelText: "Cancel");
            if (confirmed != true)
            {
                return;
            }
        }

        _busy = true;
        _errorMessage = null;
        try
        {
            var date = new DateTimeOffset(DateOnly.FromDateTime(start).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            if (_isCreating)
            {
                await ShiftsClient.CreateAsync(null, new CreateShiftRequest
                {
                    Date = date,
                    StartTime = start.TimeOfDay,
                    EndTime = end.TimeOfDay,
                    AssignedUserId = string.IsNullOrEmpty(_createAssignedUserId) ? null : _createAssignedUserId,
                });
            }
            else if (_editingShiftId is { } id)
            {
                await ShiftsClient.AdjustTimesAsync(id, null, new AdjustShiftTimesRequest
                {
                    Date = date,
                    StartTime = start.TimeOfDay,
                    EndTime = end.TimeOfDay,
                });
            }

            ClearPending();
            await LoadShiftsAsync();
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

    private async Task DeleteAsync()
    {
        if (_editingShiftId is not { } id)
        {
            return;
        }

        bool? confirmed = await DialogService.ShowMessageBox(
            "Delete shift?", "This removes the shift entirely. This can't be undone.",
            yesText: "Delete", cancelText: "Cancel");
        if (confirmed != true)
        {
            return;
        }

        _busy = true;
        _errorMessage = null;
        try
        {
            await ShiftsClient.DeleteAsync(id, null);
            ClearPending();
            await LoadShiftsAsync();
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

    private async Task OpenReassignDialog(ShiftDto shift)
    {
        var parameters = new DialogParameters<ReassignShiftDialog>
        {
            { d => d.ShiftId, shift.Id },
            { d => d.ShiftLabel, $"{FormatTimeRange(shift)} — {shift.Date:ddd, MMM d}" },
            { d => d.CurrentAssignedUserId, shift.AssignedUserId },
        };
        var result = await DialogService.ShowAsync<ReassignShiftDialog>("Reassign shift", parameters);
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadShiftsAsync();
        }
    }

    private async Task OpenRequestReplacementDialog(ShiftDto shift)
    {
        var parameters = new DialogParameters<RequestReplacementDialog>
        {
            { d => d.ShiftId, shift.Id },
            { d => d.ShiftLabel, $"{FormatTimeRange(shift)} — {shift.Date:ddd, MMM d}" },
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
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
    }

    private async Task OpenNotesDialog(ShiftDto shift)
    {
        var parameters = new DialogParameters<ShiftNotesDialog>
        {
            { d => d.ShiftId, shift.Id },
            { d => d.ShiftLabel, $"{FormatTimeRange(shift)} — {shift.Date:ddd, MMM d}" },
        };
        var result = await DialogService.ShowAsync<ShiftNotesDialog>("Notes", parameters);
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false, Data: true })
        {
            await LoadShiftsAsync();
        }
    }
}
