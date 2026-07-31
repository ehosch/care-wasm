using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;

namespace Care.Wasm.Client.Pages;

public partial class Home
{
    [Inject]
    private ISettingsClient SettingsClient { get; set; } = default!;

    private string? _patientName;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsClient.GetSettingsAsync(null);
            _patientName = settings.PatientName;
        }
        catch (ApiException)
        {
            // Non-fatal — the page just won't show the patient name.
        }
    }
}
