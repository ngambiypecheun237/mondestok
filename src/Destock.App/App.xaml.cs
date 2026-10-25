using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<LoginPage>())
        {
            Title = "Destock"
        };
        return window;
    }
}
