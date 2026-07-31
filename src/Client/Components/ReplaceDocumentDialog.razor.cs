using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Care.Wasm.Client.Components;

public partial class ReplaceDocumentDialog
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IDocumentsClient DocumentsClient { get; set; } = default!;

    [Parameter]
    public Guid DocumentId { get; set; }

    [Parameter]
    public string Title { get; set; } = string.Empty;

    private IBrowserFile? _file;
    private bool _busy;
    private string? _errorMessage;

    private void OnFileSelected(IBrowserFile file) => _file = file;

    private async Task SubmitAsync()
    {
        _errorMessage = null;

        if (_file is null)
        {
            _errorMessage = "Choose a file to upload.";
            return;
        }

        _busy = true;
        try
        {
            await using var stream = _file.OpenReadStream(MaxFileSizeBytes);
            var fileParameter = new FileParameter(stream, _file.Name, _file.ContentType);
            await DocumentsClient.ReplaceAsync(DocumentId, null, fileParameter);
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
