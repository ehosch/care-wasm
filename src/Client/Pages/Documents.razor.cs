using Care.Wasm.Client.Components;
using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Care.Wasm.Client.Pages;

public partial class Documents
{
    [Inject]
    private IDocumentsClient DocumentsClient { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    private ICollection<DocumentDto> _documents = new List<DocumentDto>();
    private bool _loading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync() => await LoadDocumentsAsync();

    private async Task LoadDocumentsAsync()
    {
        _loading = true;
        try
        {
            _documents = await DocumentsClient.GetDocumentsAsync(null);
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

    private async Task OpenUploadDialog()
    {
        var result = await DialogService.ShowAsync<UploadDocumentDialog>("Upload a document");
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadDocumentsAsync();
        }
    }

    private async Task OpenReplaceDialog(DocumentDto document)
    {
        var parameters = new DialogParameters<ReplaceDocumentDialog>
        {
            { d => d.DocumentId, document.Id },
            { d => d.Title, document.Title }
        };
        var result = await DialogService.ShowAsync<ReplaceDocumentDialog>("Replace document", parameters);
        var dialogResult = await result.Result;
        if (dialogResult is { Canceled: false })
        {
            await LoadDocumentsAsync();
        }
    }

    private async Task OpenHistoryDialog(DocumentDto document)
    {
        var parameters = new DialogParameters<VersionHistoryDialog>
        {
            { d => d.DocumentId, document.Id }
        };
        await DialogService.ShowAsync<VersionHistoryDialog>("Version history", parameters);
    }

    private async Task DownloadAsync(DocumentDto document)
    {
        try
        {
            var response = await DocumentsClient.DownloadAsync(document.Id, null);
            using var streamRef = new DotNetStreamReference(response.Stream, leaveOpen: false);
            await JsRuntime.InvokeVoidAsync("downloadFileFromStream", document.FileName, streamRef);
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
    }

    private async Task DeleteAsync(DocumentDto document)
    {
        try
        {
            await DocumentsClient.DeleteAsync(document.Id, null);
            await LoadDocumentsAsync();
        }
        catch (ApiException ex)
        {
            _errorMessage = ApiErrorHelper.GetFriendlyMessage(ex);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }
}
