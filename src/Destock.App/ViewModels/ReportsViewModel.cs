using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class ReportsViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly AppSettingsService _settings;
    private bool _suppressFilters;

    public ReportsViewModel(DestockClient client, AppSettingsService settings)
    {
        _client = client;
        _settings = settings;
        Title = "Rapports";
        CompanyName = settings.CompanyName;
    }

    public ObservableCollection<LookupOption> WarehouseOptions { get; } = [];

    [ObservableProperty]
    private LookupOption? _selectedWarehouse;

    [ObservableProperty]
    private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today;

    [ObservableProperty]
    private DashboardDto _summary = new();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _companyName = "Destock";

    public string PeriodLabel => $"Du {FromDate:dd/MM/yyyy} au {ToDate:dd/MM/yyyy}";

    public string StockValueLabel => Fmt.Money(Summary.StockValueMinor);

    public string ProductCountLabel => $"{Summary.ProductCount} produit(s)";

    public string AlertsLabel => $"{Summary.LowStockCount} alerte(s)";

    public string TodayLabel => $"{Summary.MovementsToday} mouvement(s) aujourd'hui";

    partial void OnFromDateChanged(DateTime value) => OnPropertyChanged(nameof(PeriodLabel));

    partial void OnToDateChanged(DateTime value) => OnPropertyChanged(nameof(PeriodLabel));

    partial void OnSummaryChanged(DashboardDto value)
    {
        OnPropertyChanged(nameof(StockValueLabel));
        OnPropertyChanged(nameof(ProductCountLabel));
        OnPropertyChanged(nameof(AlertsLabel));
        OnPropertyChanged(nameof(TodayLabel));
    }

    partial void OnSelectedWarehouseChanged(LookupOption? value)
    {
        if (!_suppressFilters)
        {
            _ = LoadSummaryAsync();
        }
    }

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            CompanyName = _settings.CompanyName;

            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");

            _suppressFilters = true;
            var previous = SelectedWarehouse?.Id;
            WarehouseOptions.Clear();
            WarehouseOptions.Add(new LookupOption { Id = null, Name = "Tous les dépôts" });
            foreach (var warehouse in warehouses)
            {
                WarehouseOptions.Add(new LookupOption
                {
                    Id = warehouse.Id,
                    Name = warehouse.Name,
                    Color = warehouse.IsDefault ? "#0B7A5C" : "#94A3B8"
                });
            }
            SelectedWarehouse = WarehouseOptions.FirstOrDefault(item => item.Id == previous) ?? WarehouseOptions[0];
            _suppressFilters = false;

            await LoadSummaryCoreAsync();
        });
        IsRefreshing = false;
    }

    private async Task LoadSummaryAsync()
    {
        await LoadAsync(LoadSummaryCoreAsync);
        IsRefreshing = false;
    }

    private async Task LoadSummaryCoreAsync()
    {
        Summary = await _client.CallAsync<DashboardDto>("dashboard.summary", new
        {
            WarehouseId = SelectedWarehouse?.Id
        });
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task ExportInventoryAsync()
    {
        await RunAsync(async () =>
        {
            var report = await _client.CallAsync<ReportDto>("reports.inventory", new
            {
                WarehouseId = SelectedWarehouse?.Id
            });
            await ShareAsync(report);
        }, "Export impossible");
    }

    [RelayCommand]
    private async Task ExportMovementsAsync()
    {
        await RunAsync(async () =>
        {
            if (FromDate.Date > ToDate.Date)
            {
                throw new DestockException("VALIDATION", "La date de début doit précéder la date de fin.");
            }

            var report = await _client.CallAsync<ReportDto>("reports.movements", new
            {
                From = FromDate.ToString("yyyy-MM-dd"),
                To = ToDate.ToString("yyyy-MM-dd"),
                WarehouseId = SelectedWarehouse?.Id
            });
            await ShareAsync(report);
        }, "Export impossible");
    }

    private static async Task ShareAsync(ReportDto report)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, report.Filename);
        await File.WriteAllTextAsync(path, report.Content, new UTF8Encoding(false));
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
            new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFileRequest
            {
                Title = report.Filename,
                File = new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFile(path)
            });
    }
}
