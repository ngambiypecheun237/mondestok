using System.Text.Json;
using Destock.App.Data;
using Destock.App.Models;

namespace Destock.App.Services;

public sealed class DestockException : Exception
{
    public DestockException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class DestockClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DestockDb _db;
    private readonly AuthService _auth;
    private readonly CatalogService _catalog;
    private readonly StockService _stock;
    private readonly ReportsService _reports;
    private readonly SettingsService _settings;
    private readonly DemoService _demo;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public DestockClient(DestockDb db, AuthService auth, CatalogService catalog, StockService stock, ReportsService reports, SettingsService settings, DemoService demo)
    {
        _db = db;
        _auth = auth;
        _catalog = catalog;
        _stock = stock;
        _reports = reports;
        _settings = settings;
        _demo = demo;
    }

    public bool IsInitialized => _initialized;

    public string DatabasePath => _db.DbPath;

    public string EngineVersion => "1.0.0";

    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    public async Task<T> CallAsync<T>(string operation, object? payload = null)
    {
        await InitializeAsync().ConfigureAwait(false);
        var result = await DispatchAsync(operation, payload).ConfigureAwait(false);

        if (result is JsonElement je)
        {
            return je.Deserialize<T>(JsonOptions) ?? throw new DestockException("EMPTY_RESULT", "Réponse invalide du moteur.");
        }

        if (result is T t)
        {
            return t;
        }

        var json = JsonSerializer.Serialize(result, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new DestockException("EMPTY_RESULT", "Réponse invalide du moteur.");
    }

    public async Task CallVoidAsync(string operation, object? payload = null)
    {
        await InitializeAsync().ConfigureAwait(false);
        await DispatchAsync(operation, payload).ConfigureAwait(false);
    }

    private async Task<object> DispatchAsync(string operation, object? payload)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var p = payload == null ? JsonDocument.Parse("{}").RootElement : JsonSerializer.SerializeToElement(payload, JsonOptions);

            return operation switch
            {
                "ping" => "pong",

                // Auth
                "auth.login" => _auth.Login(p),
                "auth.me" => _auth.Me(p),
                "auth.changePassword" => ExecuteVoid(() => _auth.ChangePassword(p)),
                "auth.users.list" => _auth.ListUsers(p),
                "auth.users.save" => _auth.SaveUser(p),
                "auth.users.delete" => ExecuteVoid(() => _auth.DeleteUser(p)),

                // Categories
                "categories.list" => _catalog.ListCategories(),
                "categories.save" => _catalog.SaveCategory(p),
                "categories.delete" => _catalog.DeleteCategory(p),

                // Warehouses
                "warehouses.list" => _catalog.ListWarehouses(),
                "warehouses.save" => _catalog.SaveWarehouse(p),
                "warehouses.setDefault" => _catalog.SetDefaultWarehouse(p),
                "warehouses.delete" => _catalog.DeleteWarehouse(p),

                // Products
                "products.list" => _catalog.ListProducts(p),
                "products.get" => _catalog.GetProduct(p),
                "products.lookupBarcode" => _catalog.LookupBarcode(p),
                "products.save" => _catalog.SaveProduct(p),
                "products.delete" => _catalog.DeleteProduct(p),

                // Stock/Movements
                "movements.create" => _stock.CreateMovement(p),
                "movements.list" => _stock.ListMovements(p),
                "alerts.list" => _stock.ListAlerts(p),
                "dashboard.summary" => _stock.DashboardSummary(p),

                // Reports
                "reports.inventory" => _reports.InventoryCsv(p),
                "reports.movements" => _reports.MovementsCsv(p),

                // Settings
                "settings.get" => _settings.GetAll(),
                "settings.set" => _settings.Set(p),
                "app.info" => _settings.AppInfo(DatabasePath, EngineVersion),
                "demo.seed" => ExecuteValue(() => _demo.DemoSeed()),

                _ => throw new DestockException("UNKNOWN_OP", $"Opération inconnue : {operation}")
            };
        }
        catch (DestockException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DestockException("ERROR", ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static object ExecuteVoid(Action action)
    {
        action();
        return new Models.MessageDto { Message = "OK" };
    }

    private static object ExecuteValue<T>(Func<T> func)
    {
        var result = func();
        return result!;
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
