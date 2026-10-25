using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly AppSettingsService _settings;
    private readonly SessionService _session;

    public SettingsViewModel(DestockClient client, AppSettingsService settings, SessionService session)
    {
        _client = client;
        _settings = settings;
        _session = session;
        Title = "Paramètres";

        CurrencyOptions.Add(new CurrencyOption { Code = "XOF", Label = "Franc CFA – XOF" });
        CurrencyOptions.Add(new CurrencyOption { Code = "XAF", Label = "Franc CFA – XAF" });
        CurrencyOptions.Add(new CurrencyOption { Code = "EUR", Label = "Euro – EUR" });
        CurrencyOptions.Add(new CurrencyOption { Code = "USD", Label = "Dollar US – USD" });
        CurrencyOptions.Add(new CurrencyOption { Code = "MAD", Label = "Dirham – MAD" });
        CurrencyOptions.Add(new CurrencyOption { Code = "GBP", Label = "Livre sterling – GBP" });
        CurrencyOptions.Add(new CurrencyOption { Code = "NGN", Label = "Naira – NGN" });
        CurrencyOptions.Add(new CurrencyOption { Code = "GHS", Label = "Cedi – GHS" });
        CurrencyOptions.Add(new CurrencyOption { Code = "CDF", Label = "Franc congolais – CDF" });
        CurrencyOptions.Add(new CurrencyOption { Code = "KES", Label = "Shilling – KES" });
        CurrencyOptions.Add(new CurrencyOption { Code = "CAD", Label = "Dollar canadien – CAD" });

        ThemeOptions.Add(new ChoiceOption { Value = "system", Label = "Système" });
        ThemeOptions.Add(new ChoiceOption { Value = "light", Label = "Clair" });
        ThemeOptions.Add(new ChoiceOption { Value = "dark", Label = "Sombre" });
    }

    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = [];

    public ObservableCollection<ChoiceOption> ThemeOptions { get; } = [];

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    [ObservableProperty]
    private CurrencyOption? _selectedCurrency;

    [ObservableProperty]
    private ChoiceOption? _selectedTheme;

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private string _companyName = "Destock";

    [ObservableProperty]
    private bool _barcodeSound;

    [ObservableProperty]
    private bool _barcodeVibrate;

    [ObservableProperty]
    private AppInfoDto? _info;

    [ObservableProperty]
    private bool _isRefreshing;

    public bool IsAdmin => _session.IsAdmin;

    public string UserDisplayName => _session.CurrentUser?.DisplayName ?? "Utilisateur";

    public string UserRole => _session.CurrentUser?.FriendlyRole ?? "";

    public string UserInitials => Fmt.Initials(_session.CurrentUser?.DisplayName ?? "?");

    public string UserName => _session.CurrentUser?.Username ?? "";

    public string EngineVersion => $"v{AppInfo.Current.VersionString} · moteur Rust {_client.EngineVersion}";

    public string InfoLabel => Info is null
        ? ""
        : $"{Info.ProductCount} produit(s) · {Info.MovementCount} mouvement(s) · {Info.UserCount} utilisateur(s) · {Info.WarehouseCount} dépôt(s)";

    public string DatabasePath => Info?.DbPath ?? "";

    public bool HasInfo => Info is not null;

    partial void OnInfoChanged(AppInfoDto? value)
    {
        OnPropertyChanged(nameof(InfoLabel));
        OnPropertyChanged(nameof(DatabasePath));
        OnPropertyChanged(nameof(HasInfo));
    }

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            await _settings.LoadAsync();

            CompanyName = _settings.CompanyName;            BarcodeSound = _settings.BarcodeSound;
            BarcodeVibrate = _settings.BarcodeVibrate;
            SelectedCurrency = CurrencyOptions.FirstOrDefault(item =>
                                   string.Equals(item.Code, _settings.Currency, StringComparison.OrdinalIgnoreCase))
                               ?? CurrencyOptions[0];
            SelectedTheme = ThemeOptions.FirstOrDefault(item => item.Value == _settings.Theme)
                            ?? ThemeOptions[0];

            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");
            WarehouseOptions.Clear();
            WarehouseOptions.Add(new LookupOption { Id = null, Name = "Aucun dépôt par défaut" });
            foreach (var warehouse in warehouses)
            {
                WarehouseOptions.Add(new LookupOption
                {
                    Id = warehouse.Id,
                    Name = warehouse.Name,
                    Color = warehouse.IsDefault ? "#0B7A5C" : "#94A3B8"
                });
            }
            SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == _settings.DefaultWarehouseId)
                                ?? WarehouseOptions[0];

            Info = await _client.CallAsync<AppInfoDto>("app.info");
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                throw new DestockException("VALIDATION", "Le nom de l'entreprise est obligatoire.");
            }

            await _settings.SetAsync("company_name", CompanyName.Trim());
            await _settings.SetAsync("currency", SelectedCurrency?.Code ?? "XOF");
            await _settings.SetAsync("theme", SelectedTheme?.Value ?? "system");
            await _settings.SetAsync("barcode_sound", BarcodeSound ? "true" : "false");
            await _settings.SetAsync("barcode_vibrate", BarcodeVibrate ? "true" : "false");
            await _settings.SetDefaultWarehouseAsync(SelectedWarehouse?.Id);
            await _settings.LoadAsync();

            if (Shell.Current is AppShell shell)
            {
                shell.RefreshHeader();
            }

            await Dialogs.AlertAsync("Paramètres", "Vos préférences ont été enregistrées.");
        }, "Enregistrement impossible");
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task OpenCategoriesAsync()
    {
        await Shell.Current.GoToAsync(nameof(CategoriesPage));
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        await Shell.Current.GoToAsync(nameof(ChangePasswordPage));
    }

    [RelayCommand]
    private async Task SeedDemoAsync()
    {
        if (!IsAdmin)
        {
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            "Données de démonstration",
            "Ajouter des produits et des mouvements d'exemple ? Cette action n'est proposée qu'une fois sur une base vide ou neuve.",
            "Ajouter",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("demo.seed");
            await Dialogs.AlertAsync("Démonstration", message.Message);
            Info = await _client.CallAsync<AppInfoDto>("app.info");
        }, "Ajout impossible");
    }
}
