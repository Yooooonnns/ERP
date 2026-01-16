using System.Windows.Input;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Helpers;

namespace DigitalisationERP.Desktop.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private AsyncRelayCommand? _loginCommand;

    public string Username
    {
        get => _username;
        set 
        { 
            SetField(ref _username, value);
            (_loginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string Password
    {
        get => _password;
        set 
        { 
            SetField(ref _password, value);
            (_loginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set 
        { 
            SetField(ref _isLoading, value);
            (_loginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand LoginCommand => _loginCommand ??= new AsyncRelayCommand(
        () => ExecuteLoginAsync(null),
        () => CanExecuteLogin(null)
    );

    public event EventHandler<LoginEventArgs>? LoginSucceeded;

    public LoginViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    private bool CanExecuteLogin(object? parameter)
    {
        return !IsLoading && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    }

    public async Task ExecuteLoginAsync(object? parameter)
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Logging in...";

        try
        {
            var response = await _apiClient.LoginAsync(Username, Password);
            
            if (response?.AccessToken != null)
            {
                _apiClient.AuthToken = response.AccessToken;
                StatusMessage = $"Welcome, {response.User?.Username ?? Username}!";
                LoginSucceeded?.Invoke(this, new LoginEventArgs { UserId = response.User?.UserId ?? 0, Username = response.User?.Username ?? Username });
            }
            else
            {
                StatusMessage = "Login failed. Please check your credentials.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class LoginEventArgs : EventArgs
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}
