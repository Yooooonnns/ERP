using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _accessToken;

    public string BaseUrl { get; }

    public ApiService() : this(ErpRuntimeConfig.ApiBaseUrl)
    {
    }

    public ApiService(string baseUrl)
    {
        BaseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public void SetAccessToken(string token)
    {
        _accessToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAccessToken()
    {
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    // POST request
    public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return new ApiResponse<TResponse>
                {
                    Success = true,
                    Data = data,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // POST request without response data
    public async Task<ApiResponse<object>> PostAsync<TRequest>(string endpoint, TRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, _jsonOptions);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<object>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // GET request
    public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return new ApiResponse<TResponse>
                {
                    Success = true,
                    Data = data,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // PUT request
    public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(endpoint, request, _jsonOptions);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return new ApiResponse<TResponse>
                {
                    Success = true,
                    Data = data,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // DELETE request
    public async Task<ApiResponse<object>> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<object>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Multipart upload (single file)
    public async Task<ApiResponse<TResponse>> UploadFileAsync<TResponse>(string endpoint, string filePath, string formFieldName = "file")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new ApiResponse<TResponse>
                {
                    Success = false,
                    Message = "File not found",
                    Errors = new List<string> { filePath }
                };
            }

            using var content = new MultipartFormDataContent();
            await using var fs = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fs);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, formFieldName, Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(endpoint, content);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return new ApiResponse<TResponse>
                {
                    Success = true,
                    Data = data,
                    Message = "Success"
                };
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = $"Request failed: {response.StatusCode}",
                Errors = new List<string> { errorContent }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResponse>
            {
                Success = false,
                Message = "Request failed",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
