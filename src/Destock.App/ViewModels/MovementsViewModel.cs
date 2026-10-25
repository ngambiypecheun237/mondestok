using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class MovementsViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private bool _suppressFilters;
    private bool _loaded;

    public MovementsViewModel(DestockClient client)
    {
        _client = client;
        Title = "Mouvements";

        TypeOptions.Add(new LookupOption { Id = 0, Name = "Tous les mouvements" });
        TypeOptions.Add(new LookupOption { Id = 1, Name = "Entrées" });
        TypeOptions.Add(new LookupOption { Id = 2, Name = "Sorties" });
        TypeOptions.Add(new LookupOption { Id = 3, Name = "Ajustements" });

        PeriodOptions.Add(new LookupOption { Id = 30, Name = "30 derniers jours" });
        PeriodOptions.Add(new LookupOption { Id = 7, Name = "7 derniers jours" });
        PeriodOptions.Add(new LookupOption { Id = 90, Name = "90 derniers jours" });
        PeriodOptions.Add(new LookupOption { Id = 0, Name = "Tout l'historique" });
    }

    public ObservableCollection<MovementDto> Movements { get; } = [];

    public ObservableCollection<LookupOption> TypeOptions { get; } = [];

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    public ObservableCollection<LookupOption> PeriodOptions { get; } = [];

    [ObservableProperty]
    private LookupOption? _selectedType;

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private LookupOption? _selectedPeriod;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _countLabel = "0 mouvement";

    [ObservableProperty]
    private string _inLabel = "+0 entrées";

    [ObservableProperty]
    private string _outLabel = "−0 sorties";

    [ObservableProperty]
    private string _valueLabel = "";

    public bool HasItems => Movements.Count > 0;

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            await LoadWarehousesAsync();
            await LoadMovementsCoreAsync();
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
        SelectedType ??= TypeOptions[0];
        SelectedPeriod ??= PeriodOptions[0];
        SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == previous) ?? WarehouseOptions[0];
        _suppressFilters = false;
    }

    private async Task LoadMovementsCoreAsync()
    {
        DateTime? from = null;
        if (SelectedPeriod?.Id is long days && days > 0)
        {
            from = DateTime.Today.AddDays(-days);
        }

        var type = SelectedType?.Id switch
        {
            1 => "in",
            2 => "out",
            3 => "adjust",
            _ => null
        };

        var movements = await _client.CallAsync<List<MovementDto>>("movements.list", new
        {
            From = from?.ToString("yyyy-MM-dd"),
            To = DateTime.Today.ToString("yyyy-MM-dd"),
            Type = type,
            WarehouseId = SelectedWarehouse?.Id,
            Limit = 400
        });

        Movements.Clear();
        foreach (var movement in movements)
        {
            Movements.Add(movement);
        }

        CountLabel = movements.Count == 1 ? "1 mouvement" : $"{movements.Count} mouvements";
        var stockIn = movements.Where(item => item.Type == "in").Sum(item => item.Quantity);
        var stockOut = movements.Where(item => item.Type == "out").Sum(item => item.Quantity);
        var outValue = movements.Where(item => item.Type == "out").Sum(item => item.TotalMinor);
        InLabel = $"+{Fmt.Quantity(stockIn)} entrées";
        OutLabel = $"−{Fmt.Quantity(stockOut)} sorties";
        ValueLabel = $"Sorties : {Fmt.Money(outValue)}";
        OnPropertyChanged(nameof(HasItems));
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(LoadMovementsCoreAsync);
    }

    partial void OnSelectedTypeChanged(LookupOption? value)
    {
        if (_loaded && !_suppressFilters)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnSelectedWarehouseChanged(LookupOption? value)
    {
        if (_loaded && !_suppressFilters)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnSelectedPeriodChanged(LookupOption? value)
    {
        if (_loaded && !_suppressFilters)
        {
            _ = ReloadAsync();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task NewMovementAsync()
    {
        var choice = await Dialogs.ActionSheetAsync(
            "Nouveau mouvement",
            "Annuler",
            "Entrée de stock",
            "Sortie de stock",
            "Ajustement d'inventaire");

        var type = choice switch
        {
            "Entrée de stock" => "in",
            "Sortie de stock" => "out",
            "Ajustement d'inventaire" => "adjust",
            _ => null
        };

        if (type is not null)
        {
            await Shell.Current.GoToAsync($"{nameof(NewMovementPage)}?type={type}");
        }
    }

    [RelayCommand]
    private async Task OpenMovementAsync(MovementDto movement)
    {
        var choice = await Dialogs.ActionSheetAsync(
            $"{movement.ProductName} · {movement.WarehouseName}",
            "Annuler",
            "Voir le produit",
            "Nouvelle entrée",
            "Nouvelle sortie");

        switch (choice)
        {
            case "Voir le produit":
                await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={movement.ProductId}");
                break;
            case "Nouvelle entrée":
                await Shell.Current.GoToAsync(
                    $"{nameof(NewMovementPage)}?type=in&productId={movement.ProductId}&warehouseId={movement.WarehouseId}");
                break;
            case "Nouvelle sortie":
                await Shell.Current.GoToAsync(
                    $"{nameof(NewMovementPage)}?type=out&productId={movement.ProductId}&warehouseId={movement.WarehouseId}");
                break;
        }
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
            await Shell.Current.GoToAsync(
                $"{nameof(NewMovementPage)}?type=in&productId={product.Id}");
        }
        catch (DestockException)
        {
            await Dialogs.AlertAsync(
                "Produit inconnu",
                $"Aucun produit ne correspond au code « {barcode} ».");
        }
    }
}
