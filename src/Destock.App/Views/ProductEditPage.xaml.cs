using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

[QueryProperty(nameof(ProductId), "id")]
[QueryProperty(nameof(Barcode), "barcode")]
public partial class ProductEditPage : ContentPage
{
    private readonly ProductEditViewModel _viewModel;
    private bool _initialized;
    private string _productId = "0";
    private string? _barcode;

    public ProductEditPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<ProductEditViewModel>();
        BindingContext = _viewModel;
    }

    public string ProductId
    {
        set => _productId = value;
    }

    public string Barcode
    {
        set => _barcode = Uri.UnescapeDataString(value);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        _ = long.TryParse(_productId, out var id);
        await _viewModel.InitializeAsync(id, _barcode);
        Title = _viewModel.PageHeader;
    }
}
