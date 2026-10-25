using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

[QueryProperty(nameof(UserId), "id")]
public partial class UserEditPage : ContentPage
{
    private readonly UserEditViewModel _viewModel;
    private bool _initialized;
    private string _userId = "0";

    public UserEditPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<UserEditViewModel>();
        BindingContext = _viewModel;
    }

    public string UserId
    {
        set => _userId = value;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        _ = long.TryParse(_userId, out var id);
        await _viewModel.InitializeAsync(id);
        Title = _viewModel.PageHeader;
    }
}
