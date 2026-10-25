using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class WarehouseEditViewModel : BaseViewModel
{
    private readonly DestockClient _client;

    public WarehouseEditViewModel(DestockClient client)
    {
        _client = client;
        Title = "Dépôt";
    }

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private long _warehouseId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    public string PageHeader => IsNew ? "Nouveau dépôt" : "Modifier le dépôt";

    public string SaveLabel => IsNew ? "Créer le dépôt" : "Enregistrer";

    partial void OnIsNewChanged(bool value)
    {
        OnPropertyChanged(nameof(PageHeader));
        OnPropertyChanged(nameof(SaveLabel));
    }

    public async Task InitializeAsync(long id)
    {
        await LoadAsync(async () =>
        {
            if (id <= 0)
            {
                IsNew = true;
                WarehouseId = 0;
                return;
            }

            IsNew = false;
            WarehouseId = id;
            var warehouses = await _client.CallAsync<List<WarehouseDto>>("warehouses.list");
            var warehouse = warehouses.FirstOrDefault(item => item.Id == id)
                            ?? throw new DestockException("NOT_FOUND", "Dépôt introuvable.");

            Name = warehouse.Name;
            Code = warehouse.Code;
            Address = warehouse.Address;
            Phone = warehouse.Phone;
            IsDefault = warehouse.IsDefault;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
            {
                throw new DestockException("VALIDATION", "Le nom du dépôt est obligatoire (2 caractères min.).");
            }

            var result = await _client.CallAsync<IdDto>("warehouses.save", new
            {
                Id = IsNew ? (long?)null : WarehouseId,
                Name = Name.Trim(),
                Code = string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                Address = Address.Trim(),
                Phone = Phone.Trim(),
                IsDefault
            });

            WarehouseId = result.Id;
            IsNew = false;
            await Shell.Current.GoToAsync("..");
        }, "Enregistrement impossible");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsNew)
        {
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            "Supprimer le dépôt",
            $"Voulez-vous vraiment supprimer « {Name} » ? Cette action est définitive.",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var message = await _client.CallAsync<MessageDto>("warehouses.delete", new { Id = WarehouseId });
            await Dialogs.AlertAsync("Dépôt", message.Message);
            await Shell.Current.GoToAsync("..");
        }, "Suppression impossible");
    }
}
