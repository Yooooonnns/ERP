using System;
using System.IO;
using System.Text.Json;

namespace DigitalisationERP.Core.Configuration;

public static class ErpRuntimeConfig
{
    public const string ApiBaseUrlEnvVar = "DIGITALISATIONERP_API_BASE_URL";
    public const string SettingsFileName = "digitalisationerp.settings.json";
    public const string DefaultApiBaseUrl = "http://localhost:5000";

    private static readonly Lazy<string> _apiBaseUrl = new(ResolveApiBaseUrl);

    public static string ApiBaseUrl => _apiBaseUrl.Value;

    private static string ResolveApiBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable(ApiBaseUrlEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return NormalizeBaseUrl(env);
        }

        var settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (!string.IsNullOrWhiteSpace(settings?.ApiBaseUrl))
                {
                    return NormalizeBaseUrl(settings.ApiBaseUrl);
                }
            }
            catch
            {
                // Ignore malformed settings and fall back to default.
            }
        }

        return DefaultApiBaseUrl;
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return DefaultApiBaseUrl;
        }

        // Allow users to specify host:port without scheme.
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return DefaultApiBaseUrl;
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);
        return authority.TrimEnd('/');
    }

    private sealed class Settings
    {
        public string? ApiBaseUrl { get; set; }
    }
}
