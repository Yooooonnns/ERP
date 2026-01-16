using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services;

public class AuthService
{
    private readonly ApiService _apiService;
    private LoginResponse? _currentUser;

    public AuthService(ApiService apiService)
    {
        _apiService = apiService;
    }

    public LoginResponse? CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser != null;

    public async Task<ApiResponse<LoginResponse>> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        var response = await _apiService.PostAsync<LoginRequest, LoginResponse>("/api/auth/login", request);

        if (response.Success && response.Data != null)
        {
            if (!string.IsNullOrEmpty(response.Data.AccessToken))
            {
                _currentUser = response.Data;
                _apiService.SetAccessToken(response.Data.AccessToken);
            }
        }

        return response;
    }

    public async Task<ApiResponse<object>> RegisterAsync(RegisterRequest request)
    {
        return await _apiService.PostAsync("/api/auth/register", request);
    }

    public async Task<ApiResponse<object>> VerifyEmailAsync(string token)
    {
        return await _apiService.GetAsync<object>($"/api/auth/verify-email?token={token}");
    }

    public async Task<ApiResponse<object>> ResendVerificationAsync(string email)
    {
        return await _apiService.PostAsync("/api/auth/resend-verification", new { email });
    }

    public async Task<ApiResponse<object>> ForgotPasswordAsync(string email)
    {
        return await _apiService.PostAsync("/api/auth/forgot-password", new { email });
    }

    public async Task<ApiResponse<object>> ResetPasswordAsync(string token, string newPassword)
    {
        return await _apiService.PostAsync("/api/auth/reset-password", new { token, newPassword });
    }

    public async Task<ApiResponse<object>> CreateUserAsync(CreateUserRequest request)
    {
        return await _apiService.PostAsync("/api/auth/create-user", request);
    }

    public void Logout()
    {
        _currentUser = null;
        _apiService.ClearAccessToken();
    }
}
