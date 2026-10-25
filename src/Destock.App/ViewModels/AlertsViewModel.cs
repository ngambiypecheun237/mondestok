using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class AlertsViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private bool _suppressFilters;
    private bool _loaded;

    public AlertsViewModel(DestockClient client)
    {
        _client = client;
        Title = "Alertes";
    }

    public ObservableCollection<AlertDto> Alerts { get; } = [];

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _countLabel = "0 alerte";

    public bool HasItems => Alerts.Count > 0;

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            await LoadWarehousesAsync();
            await LoadAlertsCoreAsync();
            _loaded = true;
        });
        IsRefreshing = false;
    }

    private async Task LoadWarehousesAsync()
    {
        var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");

        _suppressFilters = true;
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
        SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == previous) ?? WarehouseOptions[0];
        _suppressFilters = false;
    }

    private async Task LoadAlertsCoreAsync()
    {
        var alerts = await _client.CallAsync<List<AlertDto>>("alerts.list", new
        {
            WarehouseId = SelectedWarehouse?.Id,
            Limit = 300
        });

        Alerts.Clear();
        foreach (var alert in alerts)
        {
            Alerts.Add(alert);
        }

        CountLabel = alerts.Count == 1 ? "1 produit sous le seuil" : $"{alerts.Count} produits sous le seuil";
        OnPropertyChanged(nameof(HasItems));
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(LoadAlertsCoreAsync);
    }

    partial void OnSelectedWarehouseChanged(LookupOption? value)
    {
        if (_loaded && !_suppressFilters)
        {
            _ = ReloadAsync();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AlertTappedAsync(AlertDto alert)
    {
        var choice = await Dialogs.ActionSheetAsync(
            $"{alert.ProductName} · {alert.WarehouseName}",
            "Annuler",
            "Réapprovisionner",
            "Voir le produit");

        switch (choice)
        {
            case "Réapprovisionner":
                await Shell.Current.GoToAsync(
                    $"{nameof(NewMovementPage)}?type=in&productId={alert.ProductId}&warehouseId={alert.WarehouseId}");
                break;
            case "Voir le produit":
                await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={alert.ProductId}");
                break;
        }
    }
}
