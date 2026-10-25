using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class UsersPage : ContentPage
{
    private readonly UsersViewModel _viewModel;

    public UsersPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<UsersViewModel>();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
