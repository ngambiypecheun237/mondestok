using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class WarehousesViewModel : BaseViewModel
{
    private readonly DestockClient _client;

    public WarehousesViewModel(DestockClient client)
    {
        _client = client;
        Title = "Dépôts";
    }

    public ObservableCollection<WarehouseDto> Warehouses { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _countLabel = "0 dépôt";

    public bool HasItems => Warehouses.Count > 0;

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");
            Warehouses.Clear();
            foreach (var warehouse in warehouses)
            {
                Warehouses.Add(warehouse);
            }

            CountLabel = warehouses.Count == 1 ? "1 dépôt" : $"{warehouses.Count} dépôts";
            OnPropertyChanged(nameof(HasItems));
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AddWarehouseAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(WarehouseEditPage)}?id=0");
    }

    [RelayCommand]
    private async Task EditWarehouseAsync(WarehouseDto warehouse)
    {
        await Shell.Current.GoToAsync($"{nameof(WarehouseEditPage)}?id={warehouse.Id}");
    }

    [RelayCommand]
    private async Task SetDefaultAsync(WarehouseDto warehouse)
    {
        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("warehouses.setDefault", new { Id = warehouse.Id });
            await Dialogs.AlertAsync("Dépôt", message.Message);
            await LoadAsync();
        }, "Modification impossible");
    }

    [RelayCommand]
    private async Task DeleteWarehouseAsync(WarehouseDto warehouse)
    {
        var confirmed = await Dialogs.ConfirmAsync(
            "Supprimer le dépôt",
            $"Voulez-vous vraiment supprimer « {warehouse.Name} » ? Le dépôt ne doit contenir ni stock ni mouvement.",
            "Supprimer",
            "Annuler");

        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("warehouses.delete", new { Id = warehouse.Id });
            await Dialogs.AlertAsync("Dépôt", message.Message);
            await LoadAsync();
        }, "Suppression impossible");
    }
}
