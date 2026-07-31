using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Settings
{
    [Inject]
    private ISettingsClient SettingsClient { get; set; } = default!;

    private string _patientName = string.Empty;
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
