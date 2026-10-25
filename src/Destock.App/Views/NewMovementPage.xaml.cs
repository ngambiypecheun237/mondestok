using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

[QueryProperty(nameof(Type), "type")]
[QueryProperty(nameof(ProductId), "productId")]
[QueryProperty(nameof(WarehouseId), "warehouseId")]
[QueryProperty(nameof(Barcode), "barcode")]
public partial class NewMovementPage : ContentPage
{
    private readonly NewMovementViewModel _viewModel;
    private bool _initialized;
    private string? _type;
    private string _productId = "0";
    private string _warehouseId = "0";
    private string? _barcode;

    public NewMovementPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<NewMovementViewModel>();
        BindingContext = _viewModel;
    }

    public string Type
    {
        set => _type = value;
    }

    public string ProductId
    {
        set => _productId = value;
    }

    public string WarehouseId
    {
        set => _warehouseId = value;
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

        _ = long.TryParse(_productId, out var productId);
        _ = long.TryParse(_warehouseId, out var warehouseId);
        await _viewModel.InitializeAsync(_type, productId, warehouseId, _barcode);
    }
}
