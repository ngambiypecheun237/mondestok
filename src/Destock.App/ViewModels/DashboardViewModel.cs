using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly SessionService _session;
    private readonly AppSettingsService _settings;
    private bool _suppressFilterChange;
    private bool _firstLoad = true;

    public DashboardViewModel(DestockClient client, SessionService session, AppSettingsService settings)
    {
        _client = client;
        _session = session;
        _settings = settings;
        Title = "Accueil";
    }

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private DashboardDto _summary = new();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _greeting = "Bonjour";

    [ObservableProperty]
    private string _todayLabel = "";

    public string UserName => _session.CurrentUser?.DisplayName ?? "Utilisateur";

    public string UserInitials => Fmt.Initials(_session.CurrentUser?.DisplayName ?? "?");

    public string MonthInLabel => $"{Fmt.Quantity(Summary.StockInMonth)} entrées";

    public string MonthOutLabel => $"{Fmt.Quantity(Summary.StockOutMonth)} sorties";

    public bool HasAlerts => Summary.Alerts.Count > 0;

    public bool HasNoAlerts => !HasAlerts;

    public bool HasTopProducts => Summary.TopProducts.Count > 0;

    public bool HasRecent => Summary.RecentMovements.Count > 0;

    public bool HasNoRecent => !HasRecent;

    partial void OnSummaryChanged(DashboardDto value)
    {
        OnPropertyChanged(nameof(MonthInLabel));
        OnPropertyChanged(nameof(MonthOutLabel));
        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(HasTopProducts));
        OnPropertyChanged(nameof(HasRecent));
        OnPropertyChanged(nameof(HasNoRecent));
    }

    partial void OnSelectedWarehouseChanged(LookupOption? value)
    {
        if (_suppressFilterChange || _firstLoad)
        {
            return;
        }
        _ = LoadSummaryAsync();
    }

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            var hour = DateTime.Now.Hour;
            Greeting = hour < 12 ? "Bonjour" : hour < 18 ? "Bon après-midi" : "Bonsoir";
            TodayLabel = Capitalize(DateTime.Now.ToString("dddd d MMMM yyyy", Fmt.Fr));

            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");

            _suppressFilterChange = true;
            var previous = SelectedWarehouse?.Id;
            WarehouseOptions.Clear();
            WarehouseOptions.Add(new LookupOption { Id = null, Name = "Tous les dépôts" });
            foreach (var warehouse in warehouses)
            {
                WarehouseOptions.Add(new LookupOption
                {
                    Id = warehouse.Id,
                    Name = warehouse.Name,
                    Color = warehouse.IsDefault ? "#0B7A5C" : "#94A3B8"
                });
            }

            var preferred = previous ?? _settings.DefaultWarehouseId;
            SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == preferred)
                               ?? WarehouseOptions[0];
            _suppressFilterChange = false;

            await LoadSummaryCoreAsync();
            _firstLoad = false;
        });
        IsRefreshing = false;
    }

    private async Task LoadSummaryAsync()
    {
        await LoadAsync(LoadSummaryCoreAsync);
        IsRefreshing = false;
    }

    private async Task LoadSummaryCoreAsync()
    {
        Summary = await _client.CallAsync<DashboardDto>("dashboard.summary", new
        {
            WarehouseId = SelectedWarehouse?.Id
        });
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task NewInAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(NewMovementPage)}?type=in");
    }

    [RelayCommand]
    private async Task NewOutAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(NewMovementPage)}?type=out");
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var barcode = await ScannerService.ScanAsync();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }

        try
        {
            var product = await _client.CallAsync<ProductDetailDto>("products.lookupBarcode", new { Barcode = barcode });
            var action = await Dialogs.ActionSheetAsync(
                $"{product.Name} · stock {Fmt.Quantity(product.TotalQuantity)} {product.Unit}",
                "Annuler",
                "Nouvelle entrée",
                "Nouvelle sortie",
                "Fiche produit");

            switch (action)
            {
                case "Nouvelle entrée":
                    await Shell.Current.GoToAsync($"{nameof(NewMovementPage)}?type=in&productId={product.Id}");
                    break;
                case "Nouvelle sortie":
                    await Shell.Current.GoToAsync($"{nameof(NewMovementPage)}?type=out&productId={product.Id}");
                    break;
                case "Fiche produit":
                    await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={product.Id}");
                    break;
            }
        }
        catch (DestockException)
        {
            var create = await Dialogs.ConfirmAsync(
                "Produit inconnu",
                $"Aucun produit ne correspond au code « {barcode} ». Créer une fiche produit ?",
                "Créer",
                "Annuler");
            if (create)
            {
                await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id=0&barcode={Uri.EscapeDataString(barcode)}");
            }
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Recherche impossible", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenProductsAsync() => await Shell.Current.GoToAsync("//products");

    [RelayCommand]
    private async Task OpenAlertsAsync() => await Shell.Current.GoToAsync("//alerts");

    [RelayCommand]
    private async Task OpenMovementsAsync() => await Shell.Current.GoToAsync("//movements");

    [RelayCommand]
    private async Task OpenWarehousesAsync() => await Shell.Current.GoToAsync("//warehouses");

    [RelayCommand]
    private async Task AlertTappedAsync(AlertDto alert)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(NewMovementPage)}?type=in&productId={alert.ProductId}&warehouseId={alert.WarehouseId}");
    }

    [RelayCommand]
    private async Task ProductTappedAsync(long productId)
    {
        await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={productId}");
    }

    [RelayCommand]
    private async Task MovementTappedAsync(MovementDto movement)
    {
        await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={movement.ProductId}");
    }

    private static string Capitalize(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0]) + value[1..];
    }
}
