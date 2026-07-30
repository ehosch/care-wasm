using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Care.Wasm.Client.Components;

public partial class VersionHistoryDialog
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IDocumentsClient DocumentsClient { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Parameter]
    public Guid DocumentId { get; set; }

    private ICollection<DocumentVersionDto> _versions = new List<DocumentVersionDto>();
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        _versions = await DocumentsClient.GetVersionHistoryAsync(DocumentId, null);
        _loading = false;
    }

    private async Task DownloadVersionAsync(int version)
    {
        var response = await DocumentsClient.DownloadVersionAsync(DocumentId, version, null);
        using var streamRef = new DotNetStreamReference(response.Stream, leaveOpen: false);
        string fileName = _versions.First(v => v.Version == version).FileName;
        await JsRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }

    private void Close() => MudDialog.Close();
}
