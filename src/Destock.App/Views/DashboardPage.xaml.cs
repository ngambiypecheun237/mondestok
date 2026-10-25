using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private bool _loaded;

    public DashboardPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<DashboardViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        _loaded = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = _loaded;
    }
}
