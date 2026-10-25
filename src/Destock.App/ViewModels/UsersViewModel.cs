using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;
using Destock.App.Views;

namespace Destock.App.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly SessionService _session;

    public UsersViewModel(DestockClient client, SessionService session)
    {
        _client = client;
        _session = session;
        Title = "Utilisateurs";
    }

    public ObservableCollection<UserDto> Users { get; } = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _countLabel = "0 utilisateur";

    public bool HasItems => Users.Count > 0;

    public async Task LoadAsync()
    {
        await LoadAsync(async () =>
        {
            var users = await _client.CallAsync<List<UserDto>>("auth.users.list", new
            {
                ActorId = _session.CurrentUser?.Id
            });

            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }

            CountLabel = users.Count == 1 ? "1 utilisateur" : $"{users.Count} utilisateurs";
            OnPropertyChanged(nameof(HasItems));
        });
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task AddUserAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(UserEditPage)}?id=0");
    }

    [RelayCommand]
    private async Task EditUserAsync(UserDto user)
    {
        await Shell.Current.GoToAsync($"{nameof(UserEditPage)}?id={user.Id}");
    }
}
