using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class MovementsPage : ContentPage
{
    private readonly MovementsViewModel _viewModel;

    public MovementsPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<MovementsViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
