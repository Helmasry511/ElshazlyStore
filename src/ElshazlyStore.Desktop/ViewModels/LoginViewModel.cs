using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ElshazlyStore.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace ElshazlyStore.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Login window. Handles username/password binding and login command.
/// </summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public LoginViewModel(ISessionService sessionService, ILogger<LoginViewModel> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
        Title = Localization.Strings.Login_Title;
    }

    /// <summary>
    /// Raised when login succeeds and the shell should be shown.
    /// </summary>
    public event Action? LoginSucceeded;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = Localization.Strings.Login_PasswordRequired;
            HasError = true;
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            _logger.LogInformation("Login attempt for user {Username}", Username);

            var error = await _sessionService.LoginAsync(Username.Trim(), password);

            if (error is not null)
            {
                ErrorMessage = error;
                HasError = true;
                _logger.LogWarning("Login failed for user {Username}: {Error}", Username, error);
            }
            else
            {
                _logger.LogInformation("Login succeeded for user {Username}", Username);
                LoginSucceeded?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            ErrorMessage = Localization.Strings.State_UnexpectedError;
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !IsBusy;

    partial void OnUsernameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Called by the base class source generator. We override the property setter
    /// to also notify CanExecute since IsBusy is defined on ViewModelBase.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            LoginCommand.NotifyCanExecuteChanged();
        }
    }
}
