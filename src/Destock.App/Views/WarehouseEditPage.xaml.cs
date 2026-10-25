using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

[QueryProperty(nameof(WarehouseId), "id")]
public partial class WarehouseEditPage : ContentPage
{
    private readonly WarehouseEditViewModel _viewModel;
    private bool _initialized;
    private string _warehouseId = "0";

    public WarehouseEditPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<WarehouseEditViewModel>();
        BindingContext = _viewModel;
    }

    public string WarehouseId
    {
        set => _warehouseId = value;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        _ = long.TryParse(_warehouseId, out var id);
        await _viewModel.InitializeAsync(id);
        Title = _viewModel.PageHeader;
    }
}
