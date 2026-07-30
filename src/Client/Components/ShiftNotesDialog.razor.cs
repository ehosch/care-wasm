using Care.Wasm.Client.Infrastructure.ApiClient;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Care.Wasm.Client.Components;

public partial class ShiftNotesDialog
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private IShiftsClient ShiftsClient { get; set; } = default!;

    [Parameter]
    public Guid ShiftId { get; set; }

    [Parameter]
    public string ShiftLabel { get; set; } = string.Empty;

    private ICollection<ShiftNoteDto> _notes = new List<ShiftNoteDto>();
    private string _newNoteText = string.Empty;
    private bool _loading = true;
    private bool _posting;
    private bool _anyPosted;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _notes = await ShiftsClient.GetNotesAsync(ShiftId, null);
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

    private async Task PostAsync()
    {
        if (string.IsNullOrWhiteSpace(_newNoteText))
        {
            return;
        }

        _posting = true;
        _errorMessage = null;
        try
        {
            var note = await ShiftsClient.AddNoteAsync(ShiftId, null, new AddShiftNoteRequest { Text = _newNoteText });
            _notes.Add(note);
            _newNoteText = string.Empty;
            _anyPosted = true;
        }
        catch (ApiException ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _posting = false;
        }
    }

    private void Close() => MudDialog.Close(DialogResult.Ok(_anyPosted));
}
