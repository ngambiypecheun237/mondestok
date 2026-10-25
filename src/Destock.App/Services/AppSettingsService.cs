using Destock.App.Models;

namespace Destock.App.Services;

public sealed class AppSettingsService
{
    private readonly DestockClient _client;

    public AppSettingsService(DestockClient client)
    {
        _client = client;
    }

    public Dictionary<string, string> Values { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? SettingsChanged;

    public string Currency => Get("currency", "XOF");

    public string Theme => Get("theme", "system");

    public string CompanyName => Get("company_name", "Destock");

    public bool BarcodeSound => GetBool("barcode_sound", true);

    public bool BarcodeVibrate => GetBool("barcode_vibrate", true);

    public long? DefaultWarehouseId =>
        long.TryParse(Get("default_warehouse_id", ""), out var id) ? id : null;

    public string Get(string key, string fallback = "") =>
        Values.TryGetValue(key, out var value) ? value : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        bool.TryParse(Get(key, fallback.ToString()), out var value) ? value : fallback;

    public async Task LoadAsync()
    {
        Values = await _client.CallAsync<Dictionary<string, string>>("settings.get");
        CurrencyStore.Configure(Currency);
        ApplyTheme();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetAsync(string key, string value)
    {
        await _client.CallVoidAsync("settings.set", new { Key = key, Value = value });
        Values[key] = value;

        if (key == "currency")
        {
            CurrencyStore.Configure(value);
        }
        if (key == "theme")
        {
            ApplyTheme();
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task SetDefaultWarehouseAsync(long? id)
    {
        return SetAsync("default_warehouse_id", id?.ToString() ?? "");
    }

    public void ApplyTheme()
    {
        if (Application.Current is null)
        {
            return;
        }
        Application.Current.UserAppTheme = Theme switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
