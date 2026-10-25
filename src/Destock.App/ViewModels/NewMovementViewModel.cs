using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class NewMovementViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly AppSettingsService _settings;
    private readonly SessionService _session;
    private CancellationTokenSource? _searchCts;
    private bool _initialized;

    public NewMovementViewModel(DestockClient client, AppSettingsService settings, SessionService session)
    {
        _client = client;
        _settings = settings;
        _session = session;
        Title = "Nouveau mouvement";

        TypeOptions.Add(new ChoiceOption { Value = "in", Label = "Entrée de stock" });
        TypeOptions.Add(new ChoiceOption { Value = "out", Label = "Sortie de stock" });
        TypeOptions.Add(new ChoiceOption { Value = "adjust", Label = "Ajustement d'inventaire" });
    }

    public ObservableCollection<ChoiceOption> TypeOptions { get; } = [];

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    public ObservableCollection<ProductDto> SearchResults { get; } = [];

    [ObservableProperty]
    private ChoiceOption? _selectedTypeOption;

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private ProductDetailDto? _selectedProduct;

    [ObservableProperty]
    private string _productSearch = string.Empty;

    [ObservableProperty]
    private string _quantityText = "1";

    [ObservableProperty]
    private string _unitPriceText = string.Empty;

    [ObservableProperty]
    private string _reference = string.Empty;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _pageHeader = "Nouvelle entrée";

    [ObservableProperty]
    private string _typeHint = "Les entrées augmentent le stock du dépôt sélectionné.";

    [ObservableProperty]
    private string _quantityLabel = "Quantité reçue";

    [ObservableProperty]
    private string _priceLabel = "Prix d'achat unitaire";

    [ObservableProperty]
    private string _stockHint = string.Empty;

    [ObservableProperty]
    private string _saveLabel = "Enregistrer l'entrée";

    public bool HasProduct => SelectedProduct is not null;

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool ShowNoResult => !IsSearching &&
                                !string.IsNullOrWhiteSpace(ProductSearch) &&
                                SearchResults.Count == 0;

    public bool ShowSearchHint => string.IsNullOrWhiteSpace(ProductSearch);

    public bool IsAdjustment => SelectedTypeOption?.Value == "adjust";

    public string CurrencyHint => $"Montants en {CurrencyStore.Code}";

    public async Task InitializeAsync(string? type, long productId, long warehouseId, string? barcode)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        await LoadAsync(async () =>
        {
            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");
            WarehouseOptions.Clear();
            foreach (var warehouse in warehouses)
            {
                WarehouseOptions.Add(new LookupOption
                {
                    Id = warehouse.Id,
                    Name = warehouse.Name,
                    Color = warehouse.IsDefault ? "#0B7A5C" : "#94A3B8"
                });
            }

            var preferredWarehouse = warehouseId > 0
                ? warehouseId
                : _settings.DefaultWarehouseId
                  ?? warehouses.FirstOrDefault(item => item.IsDefault)?.Id
                  ?? warehouses.FirstOrDefault()?.Id;

            SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == preferredWarehouse)
                                 ?? WarehouseOptions.FirstOrDefault();

            SelectedTypeOption = TypeOptions.FirstOrDefault(item => item.Value == type)
                                 ?? TypeOptions[0];

            if (productId > 0)
            {
                await SelectProductAsync(productId);
            }
            else if (!string.IsNullOrWhiteSpace(barcode))
            {
                await LookupBarcodeAsync(barcode);
            }

            ApplyType();
        });
    }

    private async Task SelectProductAsync(long productId)
    {
        var detail = await _client.CallAsync<ProductDetailDto>("products.get", new { Id = productId });
        SelectedProduct = detail;
        ProductSearch = string.Empty;
        SearchResults.Clear();
        NotifySearchState();
        ApplyProductDefaults();
        UpdateStockHint();
    }

    private async Task LookupBarcodeAsync(string barcode)
    {
        try
        {
            var detail = await _client.CallAsync<ProductDetailDto>("products.lookupBarcode", new { Barcode = barcode });
            await SelectProductAsync(detail.Id);
        }
        catch (DestockException)
        {
            ProductSearch = barcode;
            await Dialogs.AlertAsync(
                "Produit inconnu",
                $"Aucun produit ne correspond au code « {barcode} ». Recherchez-le ou créez sa fiche depuis l'onglet Produits.");
        }
    }

    private void ApplyProductDefaults()
    {
        if (SelectedProduct is null)
        {
            UnitPriceText = string.Empty;
            StockHint = string.Empty;
            return;
        }

        var price = SelectedTypeOption?.Value == "out"
            ? SelectedProduct.SalePriceMinor
            : SelectedProduct.PurchasePriceMinor;
        UnitPriceText = Fmt.PriceInput(price);

        if (SelectedTypeOption?.Value == "adjust")
        {
            var stock = CurrentStock();
            QuantityText = stock.ToString("0.##", CultureInfo.InvariantCulture);
        }
        else
        {
            QuantityText = "1";
        }
    }

    private void ApplyType()
    {
        var type = SelectedTypeOption?.Value ?? "in";
        switch (type)
        {
            case "out":
                PageHeader = "Nouvelle sortie";
                TypeHint = "Les sorties diminuent le stock. La quantité ne peut pas dépasser le stock disponible.";
                QuantityLabel = "Quantité sortie";
                PriceLabel = "Prix de vente unitaire";
                SaveLabel = "Enregistrer la sortie";
                break;
            case "adjust":
                PageHeader = "Ajustement d'inventaire";
                TypeHint = "L'ajustement remplace le stock par la quantité réelle comptée. L'écart est enregistré dans le journal.";
                QuantityLabel = "Nouvelle quantité réelle";
                PriceLabel = "Prix unitaire (valorisation)";
                SaveLabel = "Valider l'ajustement";
                break;
            default:
                PageHeader = "Nouvelle entrée";
                TypeHint = "Les entrées augmentent le stock du dépôt sélectionné.";
                QuantityLabel = "Quantité reçue";
                PriceLabel = "Prix d'achat unitaire";
                SaveLabel = "Enregistrer l'entrée";
                break;
        }

        if (SelectedProduct is not null)
        {
            ApplyProductDefaults();
            UpdateStockHint();
        }

        OnPropertyChanged(nameof(IsAdjustment));
        OnPropertyChanged(nameof(CurrencyHint));
    }

    private double CurrentStock()
    {
        if (SelectedProduct is null || SelectedWarehouse is null)
        {
            return 0;
        }
        return SelectedProduct.Stocks
            .FirstOrDefault(item => item.WarehouseId == SelectedWarehouse.Id)?.Quantity ?? 0;
    }

    private void UpdateStockHint()
    {
        if (SelectedProduct is null)
        {
            StockHint = string.Empty;
            return;
        }

        var unit = SelectedProduct.Unit;
        var stock = CurrentStock();
        var warehouse = SelectedWarehouse?.Name ?? "ce dépôt";
        var total = SelectedProduct.TotalQuantity;

        StockHint = SelectedTypeOption?.Value switch
        {
            "out" => $"{Fmt.Quantity(stock)} {unit} disponible(s) dans {warehouse} · {Fmt.Quantity(total)} au total.",
            "adjust" => $"Stock actuel dans {warehouse} : {Fmt.Quantity(stock)} {unit} · {Fmt.Quantity(total)} au total.",
            _ => $"Stock actuel dans {warehouse} : {Fmt.Quantity(stock)} {unit} · {Fmt.Quantity(total)} au total."
        };
    }

    partial void OnSelectedTypeOptionChanged(ChoiceOption? value)
    {
        if (value is not null)
        {
            ApplyType();
        }
    }

    partial void OnSelectedWarehouseChanged(LookupOption? value)
    {
        UpdateStockHint();
    }

    partial void OnSelectedProductChanged(ProductDetailDto? value)
    {
        OnPropertyChanged(nameof(HasProduct));
    }

    partial void OnProductSearchChanged(string value)
    {
        _searchCts?.Cancel();
        OnPropertyChanged(nameof(ShowSearchHint));
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            NotifySearchState();
            return;
        }

        _searchCts = new CancellationTokenSource();
        _ = DebounceSearchAsync(_searchCts.Token);
    }

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNoResult));
    }

    private async Task DebounceSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(320, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            IsSearching = true;
            var products = await _client.CallAsync<List<ProductDto>>("products.list", new
            {
                Search = ProductSearch,
                Limit = 20
            });

            if (token.IsCancellationRequested)
            {
                return;
            }

            SearchResults.Clear();
            foreach (var product in products)
            {
                SearchResults.Add(product);
            }
        }
        catch (OperationCanceledException)
        {
            // Nouvelle recherche en cours.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSearching = false;
            NotifySearchState();
        }
    }

    private void NotifySearchState()
    {
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(ShowNoResult));
        OnPropertyChanged(nameof(ShowSearchHint));
    }

    [RelayCommand]
    private async Task SelectProductAsync(ProductDto product)
    {
        await LoadAsync(() => SelectProductAsync(product.Id));
    }

    [RelayCommand]
    private void ClearProduct()
    {
        SelectedProduct = null;
        UnitPriceText = string.Empty;
        StockHint = string.Empty;
        QuantityText = "1";
        ProductSearch = string.Empty;
        SearchResults.Clear();
        NotifySearchState();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var barcode = await ScannerService.ScanAsync();
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            await LoadAsync(() => LookupBarcodeAsync(barcode));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (SelectedProduct is null)
            {
                throw new DestockException("VALIDATION", "Sélectionnez d'abord un produit.");
            }
            if (SelectedWarehouse is null)
            {
                throw new DestockException("VALIDATION", "Sélectionnez un dépôt.");
            }
            if (!TryParseQuantity(QuantityText, out var quantity))
            {
                throw new DestockException("VALIDATION", "La quantité saisie est invalide.");
            }

            var type = SelectedTypeOption?.Value ?? "in";
            if (type != "adjust" && quantity <= 0)
            {
                throw new DestockException("VALIDATION", "La quantité doit être supérieure à zéro.");
            }
            if (type == "adjust" && quantity < 0)
            {
                throw new DestockException("VALIDATION", "La quantité ajustée ne peut pas être négative.");
            }

            long? unitPrice = null;
            if (!string.IsNullOrWhiteSpace(UnitPriceText))
            {
                if (!Fmt.TryParseMoney(UnitPriceText, out var parsed))
                {
                    throw new DestockException("VALIDATION", "Le prix unitaire est invalide.");
                }
                unitPrice = parsed;
            }

            var result = await _client.CallAsync<MovementDto>("movements.create", new
            {
                Type = type,
                ProductId = SelectedProduct.Id,
                WarehouseId = SelectedWarehouse.Id,
                Quantity = quantity,
                UnitPriceMinor = unitPrice,
                Reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim(),
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                UserId = _session.CurrentUser?.Id
            });

            var unit = SelectedProduct.Unit;
            var message = type switch
            {
                "in" => $"Entrée de {Fmt.Quantity(result.Quantity)} {unit} enregistrée pour « {SelectedProduct.Name} ».",
                "out" => $"Sortie de {Fmt.Quantity(result.Quantity)} {unit} enregistrée pour « {SelectedProduct.Name} ».",
                _ => $"Stock ajusté de {Fmt.Quantity(quantity - result.Quantity)} à {Fmt.Quantity(quantity)} {unit}."
            };

            await Dialogs.AlertAsync("Mouvement enregistré", message);
            await Shell.Current.GoToAsync("..");
        }, "Enregistrement impossible");
    }

    private static bool TryParseQuantity(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text
            .Replace("\u00A0", "")
            .Replace("\u202F", "")
            .Replace(" ", "")
            .Replace(',', '.');

        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }
}
