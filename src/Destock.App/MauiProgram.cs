using BarcodeScanning;
using CommunityToolkit.Mvvm.ComponentModel;
using Destock.App.Services;
using Destock.App.ViewModels;
using Destock.App.Views;
using Microsoft.Extensions.Logging;

namespace Destock.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "Inter");
                fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
                fonts.AddFont("Inter-ExtraBold.ttf", "InterExtraBold");
            });

        // Services
        builder.Services.AddSingleton<Data.DestockDb>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<CatalogService>();
        builder.Services.AddSingleton<StockService>();
        builder.Services.AddSingleton<ReportsService>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<DemoService>();
        builder.Services.AddSingleton<DestockClient>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddTransient<AppShell>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<ProductsViewModel>();
        builder.Services.AddTransient<ProductEditViewModel>();
        builder.Services.AddTransient<MovementsViewModel>();
        builder.Services.AddTransient<NewMovementViewModel>();
        builder.Services.AddTransient<AlertsViewModel>();
        builder.Services.AddTransient<WarehousesViewModel>();
        builder.Services.AddTransient<WarehouseEditViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<UserEditViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<ChangePasswordViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<ProductsPage>();
        builder.Services.AddTransient<ProductEditPage>();
        builder.Services.AddTransient<MovementsPage>();
        builder.Services.AddTransient<NewMovementPage>();
        builder.Services.AddTransient<AlertsPage>();
        builder.Services.AddTransient<WarehousesPage>();
        builder.Services.AddTransient<WarehouseEditPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<UserEditPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<ChangePasswordPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        ServiceHelper.Initialize(app.Services);
        return app;
    }
}
