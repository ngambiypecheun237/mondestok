using Destock.App.Services;
using Destock.App.ViewModels;

namespace Destock.App.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.Get<ChangePasswordViewModel>();
    }
}
