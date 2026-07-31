using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Settings
{
    [Inject]
    private ISettingsClient SettingsClient { get; set; } = default!;

    private string _patientName = string.Empty;
    private bool _notifyShiftAssignedEmail = true;
    private bool _notifyShiftAssignedSms = true;
    private bool _notifyReplacementRequestedEmail = true;
    private bool _notifyReplacementRequestedSms = true;
    private bool _notifyReplacementClaimedEmail = true;
    private bool _notifyReplacementClaimedSms = true;
    private bool _notifyDocumentUploadedEmail = true;
    private bool _notifyDocumentUploadedSms = true;
    private bool _notifyShiftRemovedEmail = true;
    private bool _notifyShiftRemovedSms = true;
    private bool _notifyShiftBoundaryChangedEmail = true;
    private bool _notifyShiftBoundaryChangedSms = true;
    private bool _notifyShiftReminderEmail = true;
    private bool _notifyShiftReminderSms = true;
    private bool _loading = true;
    private bool _busy;
    private bool _saved;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        _loading = true;
        try
        {
            var settings = await SettingsClient.GetSettingsAsync(null);
            _patientName = settings.PatientName ?? string.Empty;
            _notifyShiftAssignedEmail = settings.NotifyShiftAssignedEmail;
            _notifyShiftAssignedSms = settings.NotifyShiftAssignedSms;
            _notifyReplacementRequestedEmail = settings.NotifyReplacementRequestedEmail;
            _notifyReplacementRequestedSms = settings.NotifyReplacementRequestedSms;
            _notifyReplacementClaimedEmail = settings.NotifyReplacementClaimedEmail;
            _notifyReplacementClaimedSms = settings.NotifyReplacementClaimedSms;
            _notifyDocumentUploadedEmail = settings.NotifyDocumentUploadedEmail;
            _notifyDocumentUploadedSms = settings.NotifyDocumentUploadedSms;
            _notifyShiftRemovedEmail = settings.NotifyShiftRemovedEmail;
            _notifyShiftRemovedSms = settings.NotifyShiftRemovedSms;
            _notifyShiftBoundaryChangedEmail = settings.NotifyShiftBoundaryChangedEmail;
            _notifyShiftBoundaryChangedSms = settings.NotifyShiftBoundaryChangedSms;
            _notifyShiftReminderEmail = settings.NotifyShiftReminderEmail;
            _notifyShiftReminderSms = settings.NotifyShiftReminderSms;
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

    private async Task SaveAsync()
    {
        _busy = true;
        _saved = false;
        _errorMessage = null;
        try
        {
            await SettingsClient.UpdateSettingsAsync(null, new UpdateSettingsRequest
            {
                PatientName = string.IsNullOrWhiteSpace(_patientName) ? null : _patientName,
                NotifyShiftAssignedEmail = _notifyShiftAssignedEmail,
                NotifyShiftAssignedSms = _notifyShiftAssignedSms,
                NotifyReplacementRequestedEmail = _notifyReplacementRequestedEmail,
                NotifyReplacementRequestedSms = _notifyReplacementRequestedSms,
                NotifyReplacementClaimedEmail = _notifyReplacementClaimedEmail,
                NotifyReplacementClaimedSms = _notifyReplacementClaimedSms,
                NotifyDocumentUploadedEmail = _notifyDocumentUploadedEmail,
                NotifyDocumentUploadedSms = _notifyDocumentUploadedSms,
                NotifyShiftRemovedEmail = _notifyShiftRemovedEmail,
                NotifyShiftRemovedSms = _notifyShiftRemovedSms,
                NotifyShiftBoundaryChangedEmail = _notifyShiftBoundaryChangedEmail,
                NotifyShiftBoundaryChangedSms = _notifyShiftBoundaryChangedSms,
                NotifyShiftReminderEmail = _notifyShiftReminderEmail,
                NotifyShiftReminderSms = _notifyShiftReminderSms,
            });
            _saved = true;
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
