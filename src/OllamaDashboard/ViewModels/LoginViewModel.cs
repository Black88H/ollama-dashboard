using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly ILogger<LoginViewModel> _logger;

    /// <summary>Raised when authentication succeeds. Payload is the derived DB hex key.</summary>
    public event EventHandler<string>? AuthSucceeded;

    [ObservableProperty]
    private bool _isFirstRun;

    [ObservableProperty]
    private string _displayName = "Lernender";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _passwordConfirm = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = string.Empty;

    public LoginViewModel(IAuthService auth, ILogger<LoginViewModel> logger)
    {
        _auth   = auth;
        _logger = logger;
        IsFirstRun = !auth.HasSetup;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        if (IsFirstRun)
            await SetupAsync();
        else
            await LoginAsync();
    }

    private bool CanSubmit() => !IsBusy && !string.IsNullOrWhiteSpace(Password);

    partial void OnPasswordChanged(string value)    => SubmitCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value)        => SubmitCommand.NotifyCanExecuteChanged();

    private async Task SetupAsync()
    {
        if (Password.Length < 8)
        {
            ErrorMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return;
        }
        if (Password != PasswordConfirm)
        {
            ErrorMessage = "Die Passwörter stimmen nicht überein.";
            return;
        }

        IsBusy      = true;
        BusyMessage = "Erstelle verschlüsselte Datenbank… (kann einige Sekunden dauern)";
        try
        {
            var key = await _auth.SetupAsync(Password, DisplayName.Trim() is { Length: > 0 } n ? n : "Lernender");
            AuthSucceeded?.Invoke(this, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup failed");
            ErrorMessage = $"Einrichtung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoginAsync()
    {
        IsBusy      = true;
        BusyMessage = "Verifiziere Master-Passwort…";
        try
        {
            var key = await _auth.LoginAsync(Password);
            AuthSucceeded?.Invoke(this, key);
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Falsches Master-Passwort. Bitte versuche es erneut.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            ErrorMessage = $"Fehler beim Login: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
