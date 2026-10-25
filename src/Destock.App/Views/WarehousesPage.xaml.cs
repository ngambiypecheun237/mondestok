using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class WarehousesPage : ContentPage
{
    private readonly WarehousesViewModel _viewModel;

    public WarehousesPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<WarehousesViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
