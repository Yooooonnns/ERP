using System.Collections.ObjectModel;
using System.Windows.Input;
using DigitalisationERP.Desktop.Helpers;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop.ViewModels;

public class CreateUserViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly AuthService _authService;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _fullName = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _department = string.Empty;
    private bool _sendCredentials = true;
    private string _message = string.Empty;
    private bool _isLoading;
    private bool _isSuccess;

    public CreateUserViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        var apiService = new ApiService();
        if (!string.IsNullOrWhiteSpace(_apiClient.AuthToken))
        {
            apiService.SetAccessToken(_apiClient.AuthToken);
        }
        _authService = new AuthService(apiService);
        CreateUserCommand = new AsyncRelayCommand(
            () => CreateUserAsync(),
            () => CanCreateUser()
        );
        
        // Available roles in the system
        AvailableRoles = new ObservableCollection<RoleItem>
        {
            new() { RoleName = "S_USER", DisplayName = "S-User (Admin)", IsSelected = false },
            new() { RoleName = "PRODUCTION", DisplayName = "Production", IsSelected = false },
            new() { RoleName = "MAINTENANCE", DisplayName = "Maintenance", IsSelected = false },
            new() { RoleName = "WAREHOUSE", DisplayName = "Warehouse", IsSelected = false },
            new() { RoleName = "QUALITY", DisplayName = "Quality", IsSelected = false },
            new() { RoleName = "PLANNING", DisplayName = "Planning", IsSelected = false }
        };
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
        set => SetProperty(ref _password, value);
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

    public bool SendCredentials
    {
        get => _sendCredentials;
        set => SetProperty(ref _sendCredentials, value);
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

    public ObservableCollection<RoleItem> AvailableRoles { get; }

    public ICommand CreateUserCommand { get; }

    public event EventHandler? UserCreated;

    private bool CanCreateUser()
    {
        return !string.IsNullOrWhiteSpace(Username) &&
               !string.IsNullOrWhiteSpace(Email) &&
               !string.IsNullOrWhiteSpace(Password) &&
               AvailableRoles.Any(r => r.IsSelected) &&
               !IsLoading;
    }

    private async Task CreateUserAsync()
    {
        Message = string.Empty;
        IsSuccess = false;
        IsLoading = true;

        try
        {
            var selectedRoles = AvailableRoles.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();

            var request = new Models.CreateUserRequest
            {
                Username = Username,
                Email = Email,
                Password = Password,
                FullName = FullName,
                PhoneNumber = PhoneNumber,
                Department = Department,
                Roles = selectedRoles,
                SendCredentials = SendCredentials
            };

            var response = await _authService.CreateUserAsync(request);

            if (response.Success)
            {
                IsSuccess = true;
                Message = "User created successfully!";
                
                // Clear form
                ClearForm();
                
                UserCreated?.Invoke(this, EventArgs.Empty);
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
            Message = $"User creation failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearForm()
    {
        Username = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        FullName = string.Empty;
        PhoneNumber = string.Empty;
        Department = string.Empty;
        SendCredentials = true;
        
        foreach (var role in AvailableRoles)
        {
            role.IsSelected = false;
        }
    }
}

public class RoleItem : ViewModelBase
{
    private bool _isSelected;

    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
