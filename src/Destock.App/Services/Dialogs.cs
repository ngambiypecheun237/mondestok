namespace Destock.App.Services;

public static class Dialogs
{
    private static Page? CurrentPage =>
        Shell.Current as Page ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    public static Task AlertAsync(string title, string message, string button = "OK")
    {
        return CurrentPage?.DisplayAlertAsync(title, message, button) ?? Task.CompletedTask;
    }

    public static Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmer", string cancel = "Annuler")
    {
        return CurrentPage?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);
    }

    public static Task<string?> PromptAsync(string title, string message, string? placeholder = null, string? initialValue = null, int maxLength = 120, Keyboard? keyboard = null)
    {
        return CurrentPage?.DisplayPromptAsync(title, message, "Valider", "Annuler", placeholder, maxLength, keyboard ?? Keyboard.Default, initialValue)
               ?? Task.FromResult<string?>(null);
    }

    public static Task<string?> ActionSheetAsync(string title, string cancel, params string[] buttons)
    {
        return CurrentPage?.DisplayActionSheetAsync(title, cancel, null, buttons)
               ?? Task.FromResult<string?>(null);
    }
}
