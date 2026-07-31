using Blazored.LocalStorage;

namespace Care.Wasm.Client.Infrastructure.Theme;

public class ThemeService : IThemeService
{
    private const string StorageKey = "darkMode";

    private readonly ILocalStorageService _localStorage;

    public ThemeService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        IsDarkMode = await _localStorage.GetItemAsync<bool?>(StorageKey) == true;
    }

    public async Task ToggleAsync()
    {
        IsDarkMode = !IsDarkMode;
        await _localStorage.SetItemAsync(StorageKey, IsDarkMode);
        OnChange?.Invoke();
    }
}
