using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private string? _authToken;

    public string? AuthToken
    {
        get => _authToken;
        set => _authToken = value;
    }

    public ApiClient() : this(ErpRuntimeConfig.ApiBaseUrl)
    {
    }

    public ApiClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    private void SetAuthHeader()
    {
        if (!string.IsNullOrEmpty(_authToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new Exception($"{operation} failed: {(int)response.StatusCode} {response.ReasonPhrase}. {errorContent}");
    }

    // ============ Authentication ============
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        try
        {
            var request = new { username, password };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server returned {response.StatusCode}: {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<LoginResponse>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Login failed: {ex.Message}", ex);
        }
    }

    public async Task<RegisterResponse?> RegisterAsync(string username, string email, string password, string firstName, string lastName, string phoneNumber = "", string confirmPassword = "")
    {
        try
        {
            // Use confirmPassword parameter if provided, otherwise use password
            var actualConfirmPassword = !string.IsNullOrEmpty(confirmPassword) ? confirmPassword : password;
            
            var request = new { username, email, password, firstName, lastName, phoneNumber, confirmPassword = actualConfirmPassword };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server returned {response.StatusCode}: {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RegisterResponse>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Registration failed: {ex.Message}", ex);
        }
    }

    public void Logout()
    {
        _authToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    // ============ Users ============
    public async Task<List<UserDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"/api/users?pageNumber={pageNumber}&pageSize={pageSize}");
            await EnsureSuccessAsync(response, "Get users");

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UserListResponse>(json, _jsonOptions);
            return result?.Items ?? new List<UserDto>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch users: {ex.Message}", ex);
        }
    }

    public async Task<UserDto?> GetUserByIdAsync(long userId)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"/api/users/{userId}");
            await EnsureSuccessAsync(response, "Get user");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch user: {ex.Message}", ex);
        }
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("/api/users", request);
            await EnsureSuccessAsync(response, "Create user");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<UserDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create user: {ex.Message}", ex);
        }
    }

    public async Task<UserDto?> UpdateUserAsync(long userId, UpdateUserRequest request)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"/api/users/{userId}", request);
            await EnsureSuccessAsync(response, "Update user");
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            return JsonSerializer.Deserialize<UserDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to update user: {ex.Message}", ex);
        }
    }

    public async Task DeleteUserAsync(long userId)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.DeleteAsync($"/api/users/{userId}");
            await EnsureSuccessAsync(response, "Delete user");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete user: {ex.Message}", ex);
        }
    }

    // ============ Roles ============
    public async Task<List<RoleDto>> GetRolesAsync()
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync("/api/roles");
            await EnsureSuccessAsync(response, "Get roles");
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<RoleDto>>(json, _jsonOptions);
            return result ?? new List<RoleDto>();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch roles: {ex.Message}", ex);
        }
    }

    public async Task<RoleDto?> GetRoleByIdAsync(long roleId)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"/api/roles/{roleId}");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RoleDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch role: {ex.Message}", ex);
        }
    }

    public async Task<RoleDto?> CreateRoleAsync(CreateRoleRequest request)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.PostAsJsonAsync("/api/roles", request);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RoleDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create role: {ex.Message}", ex);
        }
    }

    public async Task<RoleDto?> UpdateRoleAsync(long roleId, UpdateRoleRequest request)
    {
        try
        {
            SetAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"/api/roles/{roleId}", request);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<RoleDto>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to update role: {ex.Message}", ex);
        }
    }

    public async Task DeleteRoleAsync(long roleId)
    {
        try
        {
            SetAuthHeader();
            await _httpClient.DeleteAsync($"/api/roles/{roleId}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete role: {ex.Message}", ex);
        }
    }

    // ============ Shifts / Scheduling ============
    public async Task<List<ShiftEntry>> GetShiftsForWeekAsync(DateTime weekStart, string? employeeId = null)
    {
        SetAuthHeader();

        var weekStartParam = Uri.EscapeDataString(weekStart.Date.ToString("yyyy-MM-dd"));
        var url = $"/api/shifts/week?weekStart={weekStartParam}";
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            url += $"&employeeId={Uri.EscapeDataString(employeeId)}";
        }

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, "Get shifts");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ShiftEntry>>(json, _jsonOptions) ?? new List<ShiftEntry>();
    }

    public async Task<ShiftEntry?> CreateShiftAsync(ShiftEntry shift)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/shifts", shift, _jsonOptions);
        await EnsureSuccessAsync(response, "Create shift");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ShiftEntry>(json, _jsonOptions);
    }

    public async Task UpsertShiftsAsync(IEnumerable<ShiftEntry> shifts)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/shifts/upsert", shifts, _jsonOptions);
        await EnsureSuccessAsync(response, "Upsert shifts");
    }

    public async Task DeleteShiftAsync(string shiftId)
    {
        if (string.IsNullOrWhiteSpace(shiftId))
        {
            return;
        }

        SetAuthHeader();
        var response = await _httpClient.DeleteAsync($"/api/shifts/{Uri.EscapeDataString(shiftId)}");
        await EnsureSuccessAsync(response, "Delete shift");
    }

    // ============ Leave Requests ============
    public async Task<List<LeaveRequestEntry>> GetLeaveRequestsAsync(string? userId = null)
    {
        SetAuthHeader();

        var url = "/api/leave-requests";
        if (!string.IsNullOrWhiteSpace(userId))
        {
            url += $"?userId={Uri.EscapeDataString(userId)}";
        }

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, "Get leave requests");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LeaveRequestEntry>>(json, _jsonOptions) ?? new List<LeaveRequestEntry>();
    }

    public async Task<LeaveRequestEntry?> CreateLeaveRequestAsync(LeaveRequestEntry request)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/leave-requests", request, _jsonOptions);
        await EnsureSuccessAsync(response, "Create leave request");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LeaveRequestEntry>(json, _jsonOptions);
    }

    public async Task UpdateLeaveRequestStatusAsync(string id, string status)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        SetAuthHeader();
        var body = new { status };
        var response = await _httpClient.PutAsJsonAsync($"/api/leave-requests/{Uri.EscapeDataString(id)}/status", body, _jsonOptions);
        await EnsureSuccessAsync(response, "Update leave request status");
    }

    // ============ Task Feedback ============
    public async Task<List<FeedbackEntry>> GetTaskFeedbackAsync(string? status = null)
    {
        SetAuthHeader();

        var url = "/api/task-feedback";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"?status={Uri.EscapeDataString(status)}";
        }

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, "Get task feedback");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<FeedbackEntry>>(json, _jsonOptions) ?? new List<FeedbackEntry>();
    }

    public async Task<FeedbackEntry?> CreateTaskFeedbackAsync(FeedbackEntry entry)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/task-feedback", entry, _jsonOptions);
        await EnsureSuccessAsync(response, "Create task feedback");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<FeedbackEntry>(json, _jsonOptions);
    }

    public async Task UpdateTaskFeedbackStatusAsync(string id, string status)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        SetAuthHeader();
        var body = new { status };
        var response = await _httpClient.PutAsJsonAsync($"/api/task-feedback/{Uri.EscapeDataString(id)}/status", body, _jsonOptions);
        await EnsureSuccessAsync(response, "Update task feedback status");
    }

    // ============ Materials / Stock Diagram ============
    public async Task<List<MaterialDto>> GetMaterialsAsync(int? materialType = null)
    {
        SetAuthHeader();
        var url = "/api/materials";
        if (materialType != null)
        {
            url += $"?type={materialType.Value}";
        }

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response, "Get materials");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<MaterialDto>>(json, _jsonOptions) ?? new List<MaterialDto>();
    }

    public async Task<MaterialDto?> UpsertMaterialAsync(MaterialUpsertRequest request)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/materials", request, _jsonOptions);
        await EnsureSuccessAsync(response, "Upsert material");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MaterialDto>(json, _jsonOptions);
    }

    public async Task<List<StockMovementDto>> GetMaterialMovementsAsync(string materialNumber, int take = 200)
    {
        SetAuthHeader();
        var mn = Uri.EscapeDataString(materialNumber ?? string.Empty);
        var response = await _httpClient.GetAsync($"/api/materials/{mn}/movements?take={take}");
        await EnsureSuccessAsync(response, "Get material movements");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<StockMovementDto>>(json, _jsonOptions) ?? new List<StockMovementDto>();
    }

    public async Task<MaterialDto?> ReceiveMaterialAsync(string materialNumber, decimal quantity, string? documentNumber = null)
    {
        SetAuthHeader();
        var mn = Uri.EscapeDataString(materialNumber ?? string.Empty);
        var body = new ReceiveMaterialRequest { quantity = quantity, documentNumber = documentNumber };
        var response = await _httpClient.PostAsJsonAsync($"/api/materials/{mn}/receive", body, _jsonOptions);
        await EnsureSuccessAsync(response, "Receive material");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MaterialDto>(json, _jsonOptions);
    }

    public async Task<List<ProductionLineDto>> GetProductionLinesAsync()
    {
        SetAuthHeader();
        var response = await _httpClient.GetAsync("/api/production-lines");
        await EnsureSuccessAsync(response, "Get production lines");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ProductionLineDto>>(json, _jsonOptions) ?? new List<ProductionLineDto>();
    }

    public async Task<ProductionLineDto?> GetProductionLineAsync(string lineId)
    {
        SetAuthHeader();
        var id = Uri.EscapeDataString(lineId ?? string.Empty);
        var response = await _httpClient.GetAsync($"/api/production-lines/{id}");
        await EnsureSuccessAsync(response, "Get production line");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductionLineDto>(json, _jsonOptions);
    }

    public async Task<ProductionLineDto?> CreateProductionLineAsync(UpsertProductionLineRequest request)
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("/api/production-lines", request, _jsonOptions);
        await EnsureSuccessAsync(response, "Create production line");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductionLineDto>(json, _jsonOptions);
    }

    public async Task<ProductionLineDto?> UpdateProductionLineAsync(string lineId, UpsertProductionLineRequest request)
    {
        SetAuthHeader();
        var id = Uri.EscapeDataString(lineId ?? string.Empty);
        var response = await _httpClient.PutAsJsonAsync($"/api/production-lines/{id}", request, _jsonOptions);
        await EnsureSuccessAsync(response, "Update production line");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductionLineDto>(json, _jsonOptions);
    }

    public async Task<ProductionLineDto?> DuplicateProductionLineAsync(string lineId, string newLineId, string? newLineName = null)
    {
        SetAuthHeader();
        var id = Uri.EscapeDataString(lineId ?? string.Empty);
        var body = new DuplicateProductionLineRequest { newLineId = newLineId, newLineName = newLineName };
        var response = await _httpClient.PostAsJsonAsync($"/api/production-lines/{id}/duplicate", body, _jsonOptions);
        await EnsureSuccessAsync(response, "Duplicate production line");
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductionLineDto>(json, _jsonOptions);
    }

    public async Task DeleteProductionLineAsync(string lineId)
    {
        SetAuthHeader();
        var id = Uri.EscapeDataString(lineId ?? string.Empty);
        var response = await _httpClient.DeleteAsync($"/api/production-lines/{id}");
        await EnsureSuccessAsync(response, "Delete production line");
    }

    public async Task PostProductionAsync(string lineId, string orderNumber, int quantity)
    {
        SetAuthHeader();
        var id = Uri.EscapeDataString(lineId ?? string.Empty);
        var body = new PostProductionRequest { orderNumber = orderNumber, quantity = quantity };
        var response = await _httpClient.PostAsJsonAsync($"/api/production-lines/{id}/produce", body, _jsonOptions);
        await EnsureSuccessAsync(response, "Post production");
    }
}

// ============ DTOs ============
// LoginResponse moved to Desktop.Models - use that instead
// CreateUserRequest moved to Desktop.Models - use that instead

public class RegisterResponse
{
    public long userId { get; set; }
    public string? username { get; set; }
    public string? email { get; set; }
}

public class UserDto
{
    public long userId { get; set; }
    public string? username { get; set; }
    public string? email { get; set; }
    public string? firstName { get; set; }
    public string? lastName { get; set; }
    public bool isActive { get; set; }
    public bool isLocked { get; set; }
    public string? department { get; set; }
    public string? phoneNumber { get; set; }
    public List<string>? roles { get; set; }
    public DateTime createdDate { get; set; }
    public DateTime? lastLoginDate { get; set; }
}

public class UserListResponse
{
    public List<UserDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class UpdateUserRequest
{
    public string? firstName { get; set; }
    public string? lastName { get; set; }
    public bool? isActive { get; set; }
}

public class RoleDto
{
    public long roleId { get; set; }
    public string? roleCode { get; set; }
    public string? roleName { get; set; }
    public string? description { get; set; }
    public bool isActive { get; set; }
    public DateTime createdDate { get; set; }
}

public class RoleListResponse
{
    public List<RoleDto> data { get; set; } = new();
}

public class CreateRoleRequest
{
    public string? roleCode { get; set; }
    public string? roleName { get; set; }
    public string? description { get; set; }
}

public class UpdateRoleRequest
{
    public string? roleName { get; set; }
    public string? description { get; set; }
    public bool? isActive { get; set; }
}

public class MaterialDto
{
    public string? materialNumber { get; set; }
    public string? description { get; set; }
    public int materialType { get; set; }
    public string? unitOfMeasure { get; set; }
    public decimal stockQuantity { get; set; }
    public decimal minimumStock { get; set; }
    public decimal maximumStock { get; set; }
}

public class StockMovementDto
{
    public string? documentNumber { get; set; }
    public string? movementType { get; set; }
    public string? materialNumber { get; set; }
    public decimal quantity { get; set; }
    public string? unitOfMeasure { get; set; }
    public string? productionOrderNumber { get; set; }
    public DateTime postingDate { get; set; }
    public int status { get; set; }
}

public class MaterialUpsertRequest
{
    public string? materialNumber { get; set; }
    public string? description { get; set; }
    public int materialType { get; set; }
    public string? unitOfMeasure { get; set; }
    public decimal? initialStockQuantity { get; set; }
    public decimal minimumStock { get; set; }
    public decimal maximumStock { get; set; }
}

public class ReceiveMaterialRequest
{
    public decimal quantity { get; set; }
    public string? documentNumber { get; set; }
}

public class ProductionLineDto
{
    public string? lineId { get; set; }
    public string? lineName { get; set; }
    public string? description { get; set; }
    public bool isActive { get; set; }
    public MaterialSummaryDto? output { get; set; }
    public List<ProductionLineInputDto> inputs { get; set; } = new();
}

public class MaterialSummaryDto
{
    public string? materialNumber { get; set; }
    public string? description { get; set; }
    public int materialType { get; set; }
    public string? unitOfMeasure { get; set; }
    public decimal stockQuantity { get; set; }
}

public class ProductionLineInputDto
{
    public string? materialNumber { get; set; }
    public string? description { get; set; }
    public decimal quantityPerUnit { get; set; }
    public string? unitOfMeasure { get; set; }
}

public class UpsertProductionLineRequest
{
    public string? lineId { get; set; }
    public string? lineName { get; set; }
    public string? description { get; set; }
    public bool isActive { get; set; }

    public MaterialRefRequest outputMaterial { get; set; } = new();
    public List<LineInputRequest>? inputs { get; set; }
}

public class LineInputRequest
{
    public MaterialRefRequest material { get; set; } = new();
    public decimal quantityPerUnit { get; set; }
    public string? unitOfMeasure { get; set; }
}

public class MaterialRefRequest
{
    public string? materialNumber { get; set; }
    public string? description { get; set; }
    public int? materialType { get; set; }
    public string? unitOfMeasure { get; set; }
    public decimal? initialStockQuantity { get; set; }
}

public class DuplicateProductionLineRequest
{
    public string? newLineId { get; set; }
    public string? newLineName { get; set; }
}

public class PostProductionRequest
{
    public string? orderNumber { get; set; }
    public decimal quantity { get; set; }
}
