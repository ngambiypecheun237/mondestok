using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class ProductsViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private CancellationTokenSource? _searchCts;
    private bool _suppressFilters;
    private bool _loaded;

    public ProductsViewModel(DestockClient client)
    {
        _client = client;
        Title = "Produits";
    }

    public ObservableCollection<ProductDto> Products { get; } = [];

    public ObservableCollection<LookupOption> CategoryOptions { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LookupOption? _selectedCategory;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _totalLabel = "0 produit";

    [ObservableProperty]
    private string _stockValueLabel = "";

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            await LoadCategoriesAsync();
            await LoadProductsCoreAsync();
            _loaded = true;
        });
        IsRefreshing = false;
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _client.CallAsync<List<CategoryDto>>("categories.list");
        _suppressFilters = true;
        var previous = SelectedCategory?.Id;
        CategoryOptions.Clear();
        CategoryOptions.Add(new LookupOption { Id = null, Name = "Toutes les catégories" });
        foreach (var category in categories)
        {
            CategoryOptions.Add(new LookupOption
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color
            });
        }
        SelectedCategory = CategoryOptions.FirstOrDefault(item => item.Id == previous) ?? CategoryOptions[0];
        _suppressFilters = false;
    }

    private async Task LoadProductsCoreAsync()
    {
        var products = await _client.CallAsync<List<ProductDto>>("products.list", new
        {
            Search = SearchText,
            CategoryId = SelectedCategory?.Id,
            LowStockOnly = LowStockOnly,
            Limit = 500
        });

        Products.Clear();
        foreach (var product in products)
        {
            Products.Add(product);
        }

        TotalLabel = products.Count == 1 ? "1 produit" : $"{products.Count} produits";
        StockValueLabel = $"Valeur : {Fmt.Money(products.Sum(item => item.StockValueMinor))}";
    }

    private async Task ReloadProductsAsync()
    {
        await LoadAsync(LoadProductsCoreAsync);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebounceSearchAsync(token);
    }

    private async Task DebounceSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                await ReloadProductsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Recherche suivante
        }
    }

    partial void OnSelectedCategoryChanged(LookupOption? value)
    {
        if (_loaded && !_suppressFilters)
        {
            _ = ReloadProductsAsync();
        }
    }

    partial void OnLowStockOnlyChanged(bool value)
    {
        if (_loaded)
        {
            _ = ReloadProductsAsync();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AddProductAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id=0");
    }

    [RelayCommand]
    private async Task OpenProductAsync(long id)
    {
        await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={id}");
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
            await Shell.Current.GoToAsync($"{nameof(ProductEditPage)}?id={product.Id}");
        }
        catch (DestockException)
        {
            var create = await Dialogs.ConfirmAsync(
                "Produit inconnu",
                $"Aucun produit ne correspond au code « {barcode} ». Créer une fiche ?",
                "Créer",
                "Annuler");
            if (create)
            {
                await Shell.Current.GoToAsync(
                    $"{nameof(ProductEditPage)}?id=0&barcode={Uri.EscapeDataString(barcode)}");
            }
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Scanner indisponible", ex.Message);
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }
}
