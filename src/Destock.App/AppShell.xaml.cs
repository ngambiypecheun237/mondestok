using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App;

public partial class AppShell : Shell
{
    private static bool _routesRegistered;

    private readonly SessionService _session;
    private readonly AppSettingsService _settings;

    public AppShell(SessionService session, AppSettingsService settings)
    {
        InitializeComponent();
        _session = session;
        _settings = settings;

        if (!_routesRegistered)
        {
            Routing.RegisterRoute(nameof(ProductEditPage), typeof(ProductEditPage));
            Routing.RegisterRoute(nameof(NewMovementPage), typeof(NewMovementPage));
            Routing.RegisterRoute(nameof(WarehouseEditPage), typeof(WarehouseEditPage));
            Routing.RegisterRoute(nameof(UserEditPage), typeof(UserEditPage));
            Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
            Routing.RegisterRoute(nameof(CategoriesPage), typeof(CategoriesPage));
            _routesRegistered = true;
        }

        UpdateHeader();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateHeader();
    }

    public void RefreshHeader() => UpdateHeader();

    private void UpdateHeader()
    {
        var user = _session.CurrentUser;
        UserNameLabel.Text = user?.DisplayName ?? "Utilisateur";
        UserInitialsLabel.Text = Fmt.Initials(user?.DisplayName ?? user?.Username);
        UserRoleLabel.Text = user?.FriendlyRole ?? "";
        CompanyLabel.Text = _settings.CompanyName;
        UsersFlyoutItem.IsVisible = _session.IsAdmin;
        VersionLabel.Text = $"v{AppInfo.Current.VersionString} · moteur Rust {ServiceHelper.Get<DestockClient>().EngineVersion}";
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var confirmed = await Dialogs.ConfirmAsync(
            "Déconnexion",
            "Voulez-vous vraiment vous déconnecter ?",
            "Se déconnecter",
            "Annuler");

        if (!confirmed)
        {
            return;
        }

        _session.Clear();
        FlyoutIsPresented = false;
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is not null)
        {
            window.Page = ServiceHelper.Get<LoginPage>();
        }
    }
}
