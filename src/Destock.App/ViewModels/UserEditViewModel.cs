using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class UserEditViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly SessionService _session;

    public UserEditViewModel(DestockClient client, SessionService session)
    {
        _client = client;
        _session = session;
        Title = "Utilisateur";

        RoleOptions.Add(new ChoiceOption { Value = "user", Label = "Utilisateur" });
        RoleOptions.Add(new ChoiceOption { Value = "admin", Label = "Administrateur" });
    }

    public ObservableCollection<ChoiceOption> RoleOptions { get; } = [];

    [ObservableProperty]
    private bool _isNew = true;

    [ObservableProperty]
    private long _userId;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private ChoiceOption? _selectedRole;

    public string PageHeader => IsNew ? "Nouvel utilisateur" : "Modifier l'utilisateur";

    public string SaveLabel => IsNew ? "Créer le compte" : "Enregistrer";

    public string PasswordLabel => IsNew ? "Mot de passe *" : "Nouveau mot de passe";

    public string PasswordHint => IsNew
        ? "4 caractères minimum. Communiquez-le à l'utilisateur."
        : "Laissez vide pour conserver le mot de passe actuel.";

    public bool IsSelf => !IsNew && UserId == _session.CurrentUser?.Id;

    public bool CanDelete => !IsNew && !IsSelf;

    partial void OnIsNewChanged(bool value)
    {
        OnPropertyChanged(nameof(PageHeader));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(PasswordLabel));
        OnPropertyChanged(nameof(PasswordHint));
        OnPropertyChanged(nameof(IsSelf));
        OnPropertyChanged(nameof(CanDelete));
    }

    partial void OnUserIdChanged(long value)
    {
        OnPropertyChanged(nameof(IsSelf));
        OnPropertyChanged(nameof(CanDelete));
    }

    public async Task InitializeAsync(long id)
    {
        await LoadAsync(async () =>
        {
            if (id <= 0)
            {
                IsNew = true;
                UserId = 0;
                SelectedRole = RoleOptions[0];
                return;
            }

            IsNew = false;
            UserId = id;
            var users = await _client.CallAsync<List<UserDto>>("auth.users.list", new
            {
                ActorId = _session.CurrentUser?.Id
            });
            var user = users.FirstOrDefault(item => item.Id == id)
                       ?? throw new DestockException("NOT_FOUND", "Utilisateur introuvable.");

            Username = user.Username;
            DisplayName = user.DisplayName;
            IsActive = user.IsActive;
            SelectedRole = RoleOptions.FirstOrDefault(item => item.Value == user.Role) ?? RoleOptions[0];
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length < 3)
            {
                throw new DestockException("VALIDATION", "Le nom d'utilisateur doit contenir au moins 3 caractères.");
            }
            if (IsNew && (string.IsNullOrWhiteSpace(Password) || Password.Length < 4))
            {
                throw new DestockException("VALIDATION", "Le mot de passe doit contenir au moins 4 caractères.");
            }
            if (!string.IsNullOrWhiteSpace(Password) && Password.Length < 4)
            {
                throw new DestockException("VALIDATION", "Le mot de passe doit contenir au moins 4 caractères.");
            }

            var result = await _client.CallAsync<UserDto>("auth.users.save", new
            {
                ActorId = _session.CurrentUser?.Id,
                Id = IsNew ? (long?)null : UserId,
                Username = Username.Trim(),
                DisplayName = DisplayName.Trim(),
                Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
                Role = SelectedRole?.Value ?? "user",
                IsActive
            });

            UserId = result.Id;
            IsNew = false;

            if (result.Id == _session.CurrentUser?.Id)
            {
                _session.SignIn(result);
                if (Shell.Current is AppShell shell)
                {
                    shell.RefreshHeader();
                }
            }

            await Shell.Current.GoToAsync("..");
        }, "Enregistrement impossible");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!CanDelete)
        {
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            "Supprimer l'utilisateur",
            $"Voulez-vous vraiment supprimer le compte « {Username} » ?",
            "Supprimer",
            "Annuler");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _client.CallVoidAsync("auth.users.delete", new
            {
                ActorId = _session.CurrentUser?.Id,
                Id = UserId
            });
            await Shell.Current.GoToAsync("..");
        }, "Suppression impossible");
    }
}
