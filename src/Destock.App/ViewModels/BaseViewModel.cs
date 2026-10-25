using CommunityToolkit.Mvvm.ComponentModel;
using Destock.App.Services;

namespace Destock.App.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _title = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    protected async Task RunAsync(Func<Task> action, string? errorTitle = null)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (DestockException ex)
        {
            ErrorMessage = ex.Message;
            await Dialogs.AlertAsync(errorTitle ?? "Action impossible", ex.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Une erreur inattendue est survenue.";
            await Dialogs.AlertAsync(errorTitle ?? "Erreur", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task LoadAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (DestockException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
