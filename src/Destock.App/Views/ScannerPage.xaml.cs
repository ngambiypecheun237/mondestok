using BarcodeScanning;
using Destock.App.Services;

namespace Destock.App.Views;

public partial class ScannerPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly bool _sound;
    private readonly bool _vibrate;
    private bool _closing;
    private bool _starting;

#if ANDROID
    private Android.Media.ToneGenerator? _tone;
#endif

    public ScannerPage(bool sound, bool vibrate)
    {
        InitializeComponent();
        _sound = sound;
        _vibrate = vibrate;

#if ANDROID
        if (_sound)
        {
            try
            {
                _tone = new Android.Media.ToneGenerator(Android.Media.Stream.Music, 80);
            }
            catch
            {
                _tone = null;
            }
        }
#endif
    }

    public Task<string?> Result => _completion.Task;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_starting || _closing)
        {
            return;
        }
        _starting = true;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                await Dialogs.AlertAsync(
                    "Caméra refusée",
                    "Autorisez l'accès à la caméra dans les réglages Android pour scanner les codes-barres.");
                await CloseAsync(null);
                return;
            }

            Camera.CameraEnabled = true;
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Caméra indisponible", ex.Message);
            await CloseAsync(null);
        }
        finally
        {
            _starting = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Camera.CameraEnabled = false;
        if (!_closing)
        {
            _closing = true;
            _completion.TrySetResult(null);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        if (_closing)
        {
            return;
        }

        var code = e.BarcodeResults
            .Select(item => item.RawValue)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        PlayFeedback();
        _ = CloseAsync(code.Trim());
    }

    private void PlayFeedback()
    {
        if (_vibrate)
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(55));
            }
            catch
            {
                // Vibration non disponible sur cet appareil.
            }
        }

#if ANDROID
        if (_sound)
        {
            try
            {
                _tone?.StartTone(Android.Media.Tone.PropBeep, 110);
            }
            catch
            {
                // Retour sonore facultatif.
            }
        }
#endif
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        Camera.TorchOn = !Camera.TorchOn;
    }

    private void OnCloseClicked(object? sender, EventArgs e)
    {
        _ = CloseAsync(null);
    }

    private async Task CloseAsync(string? result)
    {
        if (_closing)
        {
            return;
        }
        _closing = true;
        Camera.CameraEnabled = false;

        if (Navigation.ModalStack.Count > 0)
        {
            try
            {
                await Navigation.PopModalAsync();
            }
            catch
            {
                // La page a déjà été fermée.
            }
        }

        _completion.TrySetResult(result);
    }
}
