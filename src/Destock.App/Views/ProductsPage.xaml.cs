using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class ProductsPage : ContentPage
{
    private readonly ProductsViewModel _viewModel;

    public ProductsPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<ProductsViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
