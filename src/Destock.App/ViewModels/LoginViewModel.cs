using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Destock.App.Models;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly DestockClient _client;
    private readonly SessionService _session;
    private readonly AppSettingsService _settings;
    private bool _initialized;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRestoring = true;

    [ObservableProperty]
    private string? _errorMessage;

    public LoginViewModel(DestockClient client, SessionService session, AppSettingsService settings)
    {
        _client = client;
        _session = session;
        _settings = settings;
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public event EventHandler? LoginSucceeded;

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        try
        {
            Username = _session.LastUsername;
            await _client.InitializeAsync();
            await _settings.LoadAsync();

            if (await _session.TryRestoreAsync())
            {
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Impossible d'initialiser l'application : {ex.Message}";
        }
        finally
        {
            IsRestoring = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Saisissez votre nom d'utilisateur.";
            return;
        }
        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Saisissez votre mot de passe.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _client.CallAsync<UserDto>("auth.login", new
            {
                Username = Username.Trim(),
                Password = Password
            });

            _session.SignIn(user);
            await _settings.LoadAsync();
            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (DestockException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connexion impossible : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void TogglePassword()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [ObservableProperty]
    private bool _isPasswordVisible;
}
