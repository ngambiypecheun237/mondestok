using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly DestockClient _client;
    private readonly SessionService _session;

    public ChangePasswordViewModel(DestockClient client, SessionService session)
    {
        _client = client;
        _session = session;
        Title = "Mot de passe";
    }

    [ObservableProperty]
    private string _oldPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (_session.CurrentUser is null)
            {
                throw new DestockException("AUTH", "Aucune session active.");
            }
            if (string.IsNullOrEmpty(OldPassword))
            {
                throw new DestockException("VALIDATION", "Saisissez votre mot de passe actuel.");
            }
            if (NewPassword.Length < 4)
            {
                throw new DestockException("VALIDATION", "Le nouveau mot de passe doit contenir au moins 4 caractères.");
            }
            if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
            {
                throw new DestockException("VALIDATION", "Les deux mots de passe ne correspondent pas.");
            }
            if (string.Equals(OldPassword, NewPassword, StringComparison.Ordinal))
            {
                throw new DestockException("VALIDATION", "Le nouveau mot de passe doit être différent de l'ancien.");
            }

            var message = await _client.CallAsync<Models.MessageDto>("auth.changePassword", new
            {
                UserId = _session.CurrentUser.Id,
                OldPassword = OldPassword,
                NewPassword = NewPassword
            });

            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;

            await Dialogs.AlertAsync("Mot de passe", message.Message);
            await Shell.Current.GoToAsync("..");
        }, "Modification impossible");
    }
}
