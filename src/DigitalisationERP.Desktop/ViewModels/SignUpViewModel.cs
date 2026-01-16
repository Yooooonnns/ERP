using System.Text.RegularExpressions;
using System.Windows.Input;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Helpers;

namespace DigitalisationERP.Desktop.ViewModels;

public class SignUpViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _selectedDepartment = string.Empty;
    private string _selectedRole = string.Empty;
    private int _currentStep = 1;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private bool _isEmailValid;
    private bool _isPasswordValid;
    private bool _isPasswordMatching;
    private List<string> _availableRoles = new();
    private List<string> _departments = new();

    // Mapping des départements aux rôles
    private readonly Dictionary<string, List<string>> _departmentRoles = new()
    {
        { "Production", new() { "Manager Production", "Chef d'équipe", "Opérateur" } },
        { "Maintenance", new() { "Manager Maintenance", "Technicien Maintenance" } },
        { "Logistique/Entrepôt", new() { "Manager Entrepôt", "Manutentionnaire" } },
        { "Direction Générale", new() { "Directeur" } }
    };

    public string Email
    {
        get => _email;
        set
        {
            if (SetField(ref _email, value))
            {
                ValidateEmail();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetField(ref _password, value))
            {
                ValidatePassword();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetField(ref _confirmPassword, value))
            {
                ValidatePassword();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (SetField(ref _selectedDepartment, value))
            {
                UpdateAvailableRoles();
                SelectedRole = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetField(ref _selectedRole, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public List<string> AvailableRoles
    {
        get => _availableRoles;
        set => SetField(ref _availableRoles, value);
    }

    public List<string> Departments
    {
        get => _departments;
        set => SetField(ref _departments, value);
    }

    public int CurrentStep
    {
        get => _currentStep;
        set => SetField(ref _currentStep, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public bool IsEmailValid
    {
        get => _isEmailValid;
        set
        {
            if (SetField(ref _isEmailValid, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsPasswordValid
    {
        get => _isPasswordValid;
        set
        {
            if (SetField(ref _isPasswordValid, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsPasswordMatching
    {
        get => _isPasswordMatching;
        set
        {
            if (SetField(ref _isPasswordMatching, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ContinueButtonText
    {
        get => CurrentStep == 3 ? "S'inscrire" : "Continuer";
    }

    public ICommand ContinueCommand { get; }
    public ICommand BackCommand { get; }

    public event EventHandler<SignUpEventArgs>? SignUpSucceeded;

    public SignUpViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        // Initialize departments
        Departments = new() { "-- Sélectionner un département --" };
        Departments.AddRange(_departmentRoles.Keys);

        // Initialize available roles
        AvailableRoles = new() { "-- Sélectionner un rôle --" };

        ContinueCommand = new AsyncRelayCommand(
            async () => await ExecuteContinueAsync(null),
            () => CanContinue()
        );

        BackCommand = new RelayCommand(
            _ => ExecuteBack(),
            _ => CurrentStep > 1
        );
    }

    private bool CanContinue()
    {
        return CurrentStep switch
        {
            1 => IsEmailValid && !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(SelectedDepartment) && SelectedDepartment != "-- Sélectionner un département --",
            2 => !string.IsNullOrEmpty(SelectedRole) && SelectedRole != "-- Sélectionner un rôle --",
            3 => IsPasswordValid && IsPasswordMatching,
            _ => false
        };
    }

    private void ValidateEmail()
    {
        IsEmailValid = IsValidEmail(Email);
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        var emailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private void ValidatePassword()
    {
        IsPasswordValid = !string.IsNullOrEmpty(Password) && Password.Length >= 8;
        IsPasswordMatching = !string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(ConfirmPassword) && Password == ConfirmPassword;
    }

    private void UpdateAvailableRoles()
    {
        if (string.IsNullOrEmpty(SelectedDepartment) || SelectedDepartment == "-- Sélectionner un département --")
        {
            AvailableRoles = new() { "-- Sélectionner un rôle --" };
            OnPropertyChanged(nameof(AvailableRoles));
            return;
        }

        var roles = new List<string> { "-- Sélectionner un rôle --" };
        
        if (_departmentRoles.TryGetValue(SelectedDepartment, out var deptRoles))
        {
            roles.AddRange(deptRoles);
        }

        AvailableRoles = roles;
        OnPropertyChanged(nameof(AvailableRoles));
    }

    private async Task ExecuteContinueAsync(object? parameter)
    {
        if (CurrentStep < 3)
        {
            CurrentStep++;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(ContinueButtonText));
        }
        else
        {
            await ExecuteSignUpAsync(parameter);
        }
    }

    private void ExecuteBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(ContinueButtonText));
        }
    }

    private async Task ExecuteSignUpAsync(object? parameter)
    {
        if (IsLoading || !IsPasswordValid || !IsPasswordMatching)
            return;

        IsLoading = true;
        StatusMessage = "Création du compte en cours...";

        try
        {
            var response = await _apiClient.RegisterAsync(
                Email,      // username
                Email,      // email
                Password,   // password
                Email,      // firstName (using email as fallback)
                Email,      // lastName (using email as fallback)
                Email,      // phoneNumber (using email as fallback)
                ConfirmPassword  // confirmPassword
            );

            if (response?.userId > 0)
            {
                StatusMessage = "Compte créé avec succès!";
                SignUpSucceeded?.Invoke(this, new SignUpEventArgs
                {
                    UserId = response.userId,
                    Email = Email,
                    Department = SelectedDepartment,
                    Role = SelectedRole
                });
            }
            else
            {
                StatusMessage = "Erreur lors de la création du compte.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class SignUpEventArgs : EventArgs
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}


