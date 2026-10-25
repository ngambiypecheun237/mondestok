using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;

    public CategoriesPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<CategoriesViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
