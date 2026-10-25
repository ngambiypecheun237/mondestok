using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private bool _navigating;

    public LoginPage()
    {
        InitializeComponent();
        _viewModel = ServiceHelper.Get<LoginViewModel>();
        BindingContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        if (_navigating)
        {
            return;
        }
        _navigating = true;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is not null)
                {
                    window.Page = ServiceHelper.Get<AppShell>();
                }
            });
        }
        finally
        {
            _navigating = false;
        }
    }
}
