using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class AlertsPage : ContentPage
{
    private readonly AlertsViewModel _viewModel;

    public AlertsPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<AlertsViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
