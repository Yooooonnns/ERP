using System.Windows.Input;
using DigitalisationERP.Desktop.Helpers;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.ViewModels;

public class RegisterViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _fullName = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _department = string.Empty;
    private string _message = string.Empty;
    private bool _isLoading;
    private bool _isSuccess;

    public RegisterViewModel(AuthService authService)
    {
        _authService = authService;
        RegisterCommand = new RelayCommand(async _ => await RegisterAsync(), _ => CanRegister());
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string PhoneNumber
    {
        get => _phoneNumber;
        set => SetProperty(ref _phoneNumber, value);
    }

    public string Department
    {
        get => _department;
        set => SetProperty(ref _department, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetProperty(ref _isSuccess, value);
    }

    public ICommand RegisterCommand { get; }

    public event EventHandler? RegistrationSuccessful;

    private bool CanRegister()
    {
        return !string.IsNullOrWhiteSpace(Username) &&
               !string.IsNullOrWhiteSpace(Email) &&
               !string.IsNullOrWhiteSpace(Password) &&
               !string.IsNullOrWhiteSpace(ConfirmPassword) &&
               Password == ConfirmPassword &&
               !IsLoading;
    }

    private async Task RegisterAsync()
    {
        Message = string.Empty;
        IsSuccess = false;
        IsLoading = true;

        try
        {
            var request = new RegisterRequest
            {
                Username = Username,
                Email = Email,
                Password = Password,
                ConfirmPassword = ConfirmPassword,
                FullName = FullName,
                PhoneNumber = PhoneNumber,
                Department = Department
            };

            var response = await _authService.RegisterAsync(request);

            if (response.Success)
            {
                IsSuccess = true;
                Message = "Registration successful! Please check your email to verify your account.";
                RegistrationSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                IsSuccess = false;
                Message = response.Message;
                if (response.Errors.Any())
                {
                    Message += "\n" + string.Join("\n", response.Errors);
                }
            }
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            Message = $"Registration failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
