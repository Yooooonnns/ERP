using System.Windows.Input;
using DigitalisationERP.Desktop.Helpers;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.ViewModels;

public class VerifyEmailViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private string _token = string.Empty;
    private string _email = string.Empty;
    private string _message = string.Empty;
    private bool _isLoading;
    private bool _isSuccess;

    public VerifyEmailViewModel(AuthService authService)
    {
        _authService = authService;
        VerifyCommand = new RelayCommand(async _ => await VerifyAsync(), _ => CanVerify());
        ResendCommand = new RelayCommand(async _ => await ResendAsync(), _ => CanResend());
    }

    public string Token
    {
        get => _token;
        set => SetProperty(ref _token, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
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

    public ICommand VerifyCommand { get; }
    public ICommand ResendCommand { get; }

    public event EventHandler? VerificationSuccessful;

    private bool CanVerify()
    {
        return !string.IsNullOrWhiteSpace(Token) && !IsLoading;
    }

    private bool CanResend()
    {
        return !string.IsNullOrWhiteSpace(Email) && !IsLoading;
    }

    private async Task VerifyAsync()
    {
        Message = string.Empty;
        IsSuccess = false;
        IsLoading = true;

        try
        {
            var response = await _authService.VerifyEmailAsync(Token);

            if (response.Success)
            {
                IsSuccess = true;
                Message = "Email verified successfully! You can now login.";
                VerificationSuccessful?.Invoke(this, EventArgs.Empty);
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
            Message = $"Verification failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResendAsync()
    {
        Message = string.Empty;
        IsSuccess = false;
        IsLoading = true;

        try
        {
            var response = await _authService.ResendVerificationAsync(Email);

            if (response.Success)
            {
                IsSuccess = true;
                Message = "Verification email sent! Please check your inbox.";
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
            Message = $"Resend failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
