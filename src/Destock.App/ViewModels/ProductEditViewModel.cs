using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class ProductEditViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private ProductDetailDto? _detail;

    public ProductEditViewModel(DestockClient client)
    {
        _client = client;
        Title = "Produit";
    }

    public ObservableCollection<LookupOption> Categories { get; } = [];

    public ObservableCollection<StockEntryDto> Stocks { get; } = [];

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private long _productId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _barcode = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _unit = "unité";

    [ObservableProperty]
    private string _purchasePriceText = "0";

    [ObservableProperty]
    private string _salePriceText = "0";

    [ObservableProperty]
    private string _minStockText = "0";

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private LookupOption? _selectedCategory;

    [ObservableProperty]
    private string _stockLabel = string.Empty;

    [ObservableProperty]
    private bool _hasStock;

    public string PageHeader => IsNew ? "Nouveau produit" : "Modifier le produit";

    public string SaveLabel => IsNew ? "Créer le produit" : "Enregistrer";

    public string PurchasePriceLabel => $"Prix d'achat ({CurrencyStore.Code})";

    public string SalePriceLabel => $"Prix de vente ({CurrencyStore.Code})";

    public string CurrencyHint => CurrencyStore.Decimals == 0
        ? "Montants en unités entières."
        : "Montants avec décimales (ex. 12,50).";

    partial void OnIsNewChanged(bool value)
    {
        OnPropertyChanged(nameof(PageHeader));
        OnPropertyChanged(nameof(SaveLabel));
    }

    partial void OnUnitChanged(string value)
    {
        if (_detail is not null)
        {
            StockLabel = $"{Fmt.Quantity(_detail.TotalQuantity)} {value} en stock";
        }
    }

    public async Task InitializeAsync(long id, string? barcode)
    {
        await LoadAsync(async () =>
        {
            var categories = await _client.CallAsync<List<CategoryDto>>("categories.list");
            Categories.Clear();
            Categories.Add(new LookupOption { Id = null, Name = "Sans catégorie" });
            foreach (var category in categories)
            {
                Categories.Add(new LookupOption
                {
                    Id = category.Id,
                    Name = category.Name,
                    Color = category.Color
                });
            }

            if (id > 0)
            {
                IsNew = false;
                ProductId = id;
                _detail = await _client.CallAsync<ProductDetailDto>("products.get", new { Id = id });
                Name = _detail.Name;
                Sku = _detail.Sku;
                Barcode = _detail.Barcode ?? string.Empty;
                Description = _detail.Description;
                Unit = _detail.Unit;
                PurchasePriceText = Fmt.PriceInput(_detail.PurchasePriceMinor);
                SalePriceText = Fmt.PriceInput(_detail.SalePriceMinor);
                MinStockText = Fmt.Quantity(_detail.MinStock);
                IsActive = _detail.IsActive;
                SelectedCategory = Categories.FirstOrDefault(item => item.Id == _detail.CategoryId) ?? Categories[0];
                Stocks.Clear();
                foreach (var stock in _detail.Stocks)
                {
                    Stocks.Add(stock);
                }
                StockLabel = $"{Fmt.Quantity(_detail.TotalQuantity)} {_detail.Unit} en stock";
                HasStock = _detail.TotalQuantity > 0.0001;
            }
            else
            {
                IsNew = true;
                ProductId = 0;
                SelectedCategory = Categories[0];
                Barcode = barcode ?? string.Empty;
            }

            OnPropertyChanged(nameof(PurchasePriceLabel));
            OnPropertyChanged(nameof(SalePriceLabel));
            OnPropertyChanged(nameof(CurrencyHint));
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
            {
                throw new DestockException("VALIDATION", "Le nom du produit est obligatoire (2 caractères min.).");
            }
            if (string.IsNullOrWhiteSpace(Sku))
            {
                throw new DestockException("VALIDATION", "La référence (SKU) est obligatoire.");
            }
            if (!Fmt.TryParseMoney(PurchasePriceText, out var purchase))
            {
                throw new DestockException("VALIDATION", "Le prix d'achat est invalide.");
            }
            if (!Fmt.TryParseMoney(SalePriceText, out var sale))
            {
                throw new DestockException("VALIDATION", "Le prix de vente est invalide.");
            }
            if (!TryParseQuantity(MinStockText, out var minStock) || minStock < 0)
            {
                throw new DestockException("VALIDATION", "Le seuil d'alerte est invalide.");
            }

            var result = await _client.CallAsync<IdDto>("products.save", new
            {
                Id = IsNew ? (long?)null : ProductId,
                Sku = Sku.Trim(),
                Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                Name = Name.Trim(),
                Description = Description,
                CategoryId = SelectedCategory?.Id,
                Unit = string.IsNullOrWhiteSpace(Unit) ? "unité" : Unit.Trim(),
                PurchasePriceMinor = purchase,
                SalePriceMinor = sale,
                MinStock = minStock,
                IsActive
            });

            ProductId = result.Id;
            IsNew = false;
            await Shell.Current.GoToAsync("..");
        }, "Enregistrement impossible");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew)
        {
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            "Supprimer le produit",
            $"Voulez-vous vraiment supprimer « {Name} » ? Cette action est irréversible.",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("products.delete", new { Id = ProductId });
            await Dialogs.AlertAsync("Produit", message.Message);
            await Shell.Current.GoToAsync("..");
        }, "Suppression impossible");
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var code = await ScannerService.ScanAsync();
        if (!string.IsNullOrWhiteSpace(code))
        {
            Barcode = code;
        }
    }

    private static bool TryParseQuantity(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var cleaned = text.Replace(',', '.');
        return double.TryParse(cleaned, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
