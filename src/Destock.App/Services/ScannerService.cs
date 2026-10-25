using Destock.App.Views;

namespace Destock.App.Services;

/// <summary>Ouvre le scanner modal et attend le premier code-barres détecté.</summary>
public static class ScannerService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<string?> ScanAsync()
    {
        if (Shell.Current is null)
        {
            return null;
        }

        await Gate.WaitAsync();
        try
        {
            var settings = ServiceHelper.Get<AppSettingsService>();
            var page = new ScannerPage(settings.BarcodeSound, settings.BarcodeVibrate);
            await Shell.Current.Navigation.PushModalAsync(page);
            return await page.Result;
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Scanner indisponible", ex.Message);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }
}
