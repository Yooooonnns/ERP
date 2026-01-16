using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DigitalisationERP.Core.Configuration;

namespace DigitalisationERP.Launcher;

public partial class App : System.Windows.Application
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
        "DigitalisationERP_Launcher.log");

    public App()
    {
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogMessage("=== Launcher Startup ===");

        var configuredBaseUrl = ResolveConfiguredApiBaseUrl();
        var selectedBaseUrl = configuredBaseUrl;

        // Ensure API is running before launching Desktop.
        try
        {
            // Hard timeout so Launcher never appears to "do nothing".
            var ensureTask = EnsureApiRunningAsync(configuredBaseUrl);
            if (!ensureTask.Wait(TimeSpan.FromSeconds(20)))
            {
                LogMessage("API warmup check timed out; continuing to launch Desktop.");
            }
            else
            {
                selectedBaseUrl = ensureTask.GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"API startup via Launcher failed: {ex.Message}");
            // Don't hard-fail launcher: Desktop has its own startup logic.
        }
        
        // Lancer Desktop (qui gère le splash et l'API)
        LogMessage("Launching Desktop...");
        var desktopExePath = ResolveDesktopExePath();
        
        if (File.Exists(desktopExePath))
        {
            var desktopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = desktopExePath,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };

            desktopProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(desktopExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            desktopProcess.StartInfo.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = selectedBaseUrl;

            try
            {
                desktopProcess.Start();
                LogMessage("Desktop launched successfully: " + desktopExePath);
            }
            catch (Exception ex)
            {
                LogMessage($"Error launching Desktop: {ex.Message}");
            }
        }
        else
        {
            LogMessage("Desktop.exe not found. Resolved path was: " + desktopExePath);
            MessageBox.Show(
                "DigitalisationERP Desktop executable was not found next to the Launcher.\n\n" +
                "Fix: publish/copy DigitalisationERP.Desktop.exe into the same folder as the Launcher (or a 'Desktop' subfolder).",
                "Launch error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        
        // Fermer le Launcher immédiatement
        this.Shutdown(0);
    }

    private static async Task<string> EnsureApiRunningAsync(string configuredBaseUrl)
    {
        var baseUrl = NormalizeBaseUrl(configuredBaseUrl);
        if (await IsApiHealthyAsync(baseUrl).ConfigureAwait(false))
        {
            // If we can resolve API artifacts near the Launcher, prefer running our own instance
            // (avoids accidentally reusing a stale API from another folder/session).
            var resolvedStartInfo = ResolveApiStartInfo(baseUrl);
            if (resolvedStartInfo == null)
            {
                LogMessage("API already running; launcher will not start a new instance.");
                return baseUrl;
            }

            LogMessage("API already running; starting a dedicated instance from Launcher artifacts on a fallback port.");
            baseUrl = FindFallbackBaseUrl(baseUrl);
            resolvedStartInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
            resolvedStartInfo.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = baseUrl;

            try
            {
                var proc = new Process { StartInfo = resolvedStartInfo };
                proc.Start();
                LogMessage($"API started by Launcher on fallback port (PID: {proc.Id})");

                try
                {
                    proc.OutputDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                            LogMessage("[API stdout] " + args.Data);
                    };
                    proc.ErrorDataReceived += (_, args) =>
                    {
                        if (!string.IsNullOrWhiteSpace(args.Data))
                            LogMessage("[API stderr] " + args.Data);
                    };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                }
                catch
                {
                    // ignore
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to start fallback API process: {ex.Message}");
                return baseUrl;
            }

            if (await WaitForApiHealthyAsync(baseUrl, attempts: 60, delayMs: 250).ConfigureAwait(false))
            {
                LogMessage("Fallback API health check passed.");
            }
            else
            {
                LogMessage("Fallback API health check timed out.");
            }

            return baseUrl;
        }

        // If the configured port is already occupied, first give the existing process a moment
        // to become healthy; only fall back if /health never responds.
        if (TryGetPort(baseUrl, out var port) && IsTcpPortInUse(port))
        {
            LogMessage($"Configured API port {port} is already in use; waiting briefly for /health...");
            if (await WaitForApiHealthyAsync(baseUrl, attempts: 20, delayMs: 250).ConfigureAwait(false))
            {
                LogMessage("Existing API became healthy; reusing it.");
                return baseUrl;
            }

            LogMessage($"/health did not respond on port {port}; selecting a fallback port.");
            baseUrl = FindFallbackBaseUrl(baseUrl);
            LogMessage($"Using API base URL: {baseUrl}");
        }

        var startInfo = ResolveApiStartInfo(baseUrl);
        if (startInfo == null)
        {
            LogMessage("API artifacts not found near Launcher; skipping API start.");
            return baseUrl;
        }

        try
        {
            var proc = new Process { StartInfo = startInfo };
            proc.Start();
            LogMessage($"API started by Launcher (PID: {proc.Id})");

            // Best-effort log capture for diagnostics.
            try
            {
                proc.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        LogMessage("[API stdout] " + args.Data);
                };
                proc.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                        LogMessage("[API stderr] " + args.Data);
                };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch
            {
                // ignore
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to start API process: {ex.Message}");
            return baseUrl;
        }

        // Wait briefly for API to come up.
        for (var i = 0; i < 60; i++)
        {
            if (await IsApiHealthyAsync(baseUrl).ConfigureAwait(false))
            {
                LogMessage("API health check passed.");
                return baseUrl;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }

        LogMessage("API health check timed out.");
        return baseUrl;
    }

    private static bool TryGetPort(string baseUrl, out int port)
    {
        port = 0;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        port = uri.Port;
        return port > 0;
    }

    private static bool IsTcpPortInUse(int port)
    {
        try
        {
            foreach (var endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                if (endpoint.Port == port)
                {
                    return true;
                }
            }
        }
        catch
        {
            // If we can't determine, assume it's free and let start attempt decide.
        }

        return false;
    }

    private static async Task<bool> IsApiHealthyAsync(string baseUrl)
    {
        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                Proxy = null
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            using var response = await httpClient
                .GetAsync($"{baseUrl.TrimEnd('/')}/health", HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForApiHealthyAsync(string baseUrl, int attempts, int delayMs)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (await IsApiHealthyAsync(baseUrl).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(delayMs).ConfigureAwait(false);
        }

        return false;
    }

    private static ProcessStartInfo? ResolveApiStartInfo(string baseUrl)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Portable/published layouts near launcher
        var candidates = new[]
        {
            Path.Combine(baseDir, "DigitalisationERP.API.exe"),
            Path.Combine(baseDir, "DigitalisationERP.API.dll"),
            Path.Combine(baseDir, "API", "DigitalisationERP.API.exe"),
            Path.Combine(baseDir, "API", "DigitalisationERP.API.dll"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "DigitalisationERP.API.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "DigitalisationERP.API.dll")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "API", "DigitalisationERP.API.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "API", "DigitalisationERP.API.dll"))
        };

        foreach (var c in candidates)
        {
            try
            {
                if (!File.Exists(c))
                {
                    continue;
                }

                var useDotnetHost = c.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                var psi = new ProcessStartInfo
                {
                    FileName = useDotnetHost ? "dotnet" : c,
                    Arguments = useDotnetHost ? $"\"{c}\"" : string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(c) ?? baseDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                psi.Environment["ASPNETCORE_URLS"] = baseUrl;
                psi.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = baseUrl;
                LogMessage("Resolved API artifact near Launcher: " + c);
                return psi;
            }
            catch
            {
                // ignore
            }
        }

        // Repo fallback (Launcher running from within the repo)
        var projectRoot = FindProjectRoot(baseDir);
        var publishExe = Path.Combine(projectRoot, "src", "DigitalisationERP.API", "bin", "Release", "net9.0", "win-x64", "publish", "DigitalisationERP.API.exe");
        var debugDll = Path.Combine(projectRoot, "src", "DigitalisationERP.API", "bin", "Debug", "net9.0", "DigitalisationERP.API.dll");

        if (File.Exists(publishExe))
        {
            var psi = new ProcessStartInfo
            {
                FileName = publishExe,
                WorkingDirectory = Path.GetDirectoryName(publishExe) ?? projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["ASPNETCORE_URLS"] = baseUrl;
            psi.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = baseUrl;
            LogMessage("Resolved API publish exe in repo: " + publishExe);
            return psi;
        }

        if (File.Exists(debugDll))
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{debugDll}\"",
                WorkingDirectory = Path.GetDirectoryName(debugDll) ?? projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["ASPNETCORE_URLS"] = baseUrl;
            psi.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = baseUrl;
            LogMessage("Resolved API debug dll in repo: " + debugDll);
            return psi;
        }

        return null;
    }

    private static string ResolveConfiguredApiBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable(ErpRuntimeConfig.ApiBaseUrlEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return NormalizeBaseUrl(env);
        }

        var settingsPath = Path.Combine(AppContext.BaseDirectory, ErpRuntimeConfig.SettingsFileName);
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
                // ignore
            }
        }

        return ErpRuntimeConfig.DefaultApiBaseUrl;
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return ErpRuntimeConfig.DefaultApiBaseUrl;
        }

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "http://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return ErpRuntimeConfig.DefaultApiBaseUrl;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string FindFallbackBaseUrl(string configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri))
        {
            return configuredBaseUrl;
        }

        for (var i = 1; i <= 20; i++)
        {
            var candidatePort = uri.Port + i;
            if (IsTcpPortInUse(candidatePort))
            {
                continue;
            }

            return new UriBuilder(uri) { Port = candidatePort }.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return configuredBaseUrl;
    }

    private sealed class Settings
    {
        public string? ApiBaseUrl { get; set; }
    }

    private static string FindProjectRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DigitalisationERP.sln")))
            {
                LogMessage("Resolved project root: " + directory.FullName);
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string ResolveDesktopExePath()
    {
        // Resolve relative to the Launcher's own location (works for installed/published builds).
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Common deployment layouts we support:
        // 1) Same folder:   Launcher.exe + Desktop.exe
        // 2) Subfolder:     Launcher.exe + Desktop\Desktop.exe
        // 3) Parent folder: Launcher\Launcher.exe and Desktop.exe in parent
        var candidates = new[]
        {
            Path.Combine(baseDir, "DigitalisationERP.Desktop.exe"),
            Path.Combine(baseDir, "Desktop", "DigitalisationERP.Desktop.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "DigitalisationERP.Desktop.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "Desktop", "DigitalisationERP.Desktop.exe"))
        };

        foreach (var c in candidates)
        {
            try
            {
                if (File.Exists(c))
                {
                    LogMessage("Resolved Desktop exe: " + c);
                    return c;
                }
            }
            catch
            {
                // ignore invalid path
            }
        }

        // If not found, return the first candidate as the "expected" path.
        return candidates.First();
    }

    private static void KillExistingDotnetProcesses()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("dotnet"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }
    }

    private static void LogMessage(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
