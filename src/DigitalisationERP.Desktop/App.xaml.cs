using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private Process? _apiProcess;
    private string _apiBaseUrl = ErpRuntimeConfig.DefaultApiBaseUrl;
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
        "DigitalisationERP_Desktop.log");

    public App()
    {
        // Handle exceptions on any thread
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                LogMessage($"Fatal: {ex?.Message}\n{ex?.StackTrace}");
                MessageBox.Show($"Fatal Error: {ex?.Message}\n\n{ex?.StackTrace}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        };

        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                LogMessage($"UI Error: {e.Exception?.Message}");
                MessageBox.Show($"UI Error: {e.Exception?.Message}\n\n{e.Exception?.StackTrace}", "UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = false;
            }
            catch { }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            LogMessage("=== Desktop OnStartup ===");

            // Resolve API base URL early (before any ErpRuntimeConfig.ApiBaseUrl evaluation), so we can
            // avoid startup crashes when the configured port is already used by another process.
            _apiBaseUrl = ResolveAndSelectApiBaseUrl();
            Environment.SetEnvironmentVariable(ErpRuntimeConfig.ApiBaseUrlEnvVar, _apiBaseUrl);
            LogMessage($"API base URL for this session: {_apiBaseUrl}");
            
            // Set ShutdownMode to close when last window closes
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            
            // Le SplashScreen est déjà affiché par StartupUri
            // Démarrer l'API en arrière-plan et attendre qu'elle soit prête
            Task.Run(async () =>
            {
                try
                {
                    LogMessage("Starting API...");
                    await StartApiAsync();
                    LogMessage("API started successfully, transitioning to login...");
                    
                    // Une fois l'API démarrée, passer à la fenêtre de login
                    Dispatcher.Invoke(() =>
                    {
                        // Créer et afficher la fenêtre de login AVANT de fermer le splash
                        var apiClient = new Services.ApiClient(_apiBaseUrl);
                        var loginWindow = new LoginWindow(apiClient);
                        
                        // Définir LoginWindow comme nouvelle MainWindow AVANT de l'afficher
                        MainWindow = loginWindow;
                        loginWindow.Show();
                        
                        // Maintenant fermer le splash screen en toute sécurité
                        // Trouver et fermer toutes les fenêtres SplashScreen
                        foreach (Window window in Windows)
                        {
                            if (window is SplashScreen splash)
                            {
                                splash.Close();
                                break;
                            }
                        }
                        
                        LogMessage("LoginWindow shown");
                    });
                }
                catch (Exception ex)
                {
                    LogMessage($"API startup failed: {ex.Message}");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Erreur lors du démarrage de l'API: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    });
                }
            });
        }
        catch (Exception ex)
        {
            LogMessage($"Startup Error: {ex.Message}");
            MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task StartApiAsync()
    {
        LogMessage("Starting API process...");

        // If API already responds, skip spawning another process.
        // Note: first startup can take a few seconds (DB init + seeding), so we wait a bit longer.
        if (await WaitForApiReadyAsync(30, 500))
        {
            LogMessage("API already running; reusing existing instance.");
            return;
        }

        // If the configured port is already in use, do NOT try to start another API instance.
        // This usually means an existing API is still warming up; spawning another instance will fail with
        // "address already in use".
        if (TryGetConfiguredPort(out var port) && IsTcpPortInUse(port))
        {
            LogMessage($"API port {port} is already in use. Waiting for existing instance to become healthy...");
            if (await WaitForApiReadyAsync(120, 500))
            {
                LogMessage("Existing API became healthy; reusing it.");
                return;
            }

            throw new Exception($"Le port {port} est déjà utilisé et l'API ne répond pas sur {ErpRuntimeConfig.ApiBaseUrl}. " +
                                "Ferme l'autre instance de l'API (ou change DIGITALISATIONERP_API_BASE_URL / digitalisationerp.settings.json). ");
        }

        _apiProcess = StartApiProcess();

        // Attendre que l'API soit prête
        LogMessage("Waiting for API to be ready...");
        bool apiReady = await WaitForApiReadyAsync();

        if (!apiReady)
        {
            throw new Exception("L'API n'a pas pu démarrer dans les temps impartis");
        }
        
        LogMessage("API is ready!");
    }

    private async Task InitializeApiAndShowLoginAsync()
    {
        try
        {
            LogMessage("Starting API...");
            _apiProcess = StartApiProcess();
            
            // Attendre que l'API soit prête (pendant que le splash s'affiche)
            LogMessage("Waiting for API to be ready...");
            bool apiReady = await WaitForApiReadyAsync();

            if (!apiReady)
            {
                LogMessage("API failed to start");
                if (_apiProcess != null && !_apiProcess.HasExited)
                    _apiProcess.Kill();
                
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Failed to start API", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                });
                return;
            }

            LogMessage("API ready!");
            
            // Maintenant afficher LoginWindow
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Fermer le splash screen
                    if (System.Windows.Application.Current.MainWindow is SplashScreen splash)
                    {
                        splash.Close();
                    }

                    // Créer et afficher LoginWindow
                    var apiClient = new ApiClient(_apiBaseUrl);
                    var loginWindow = new LoginWindow(apiClient);
                    loginWindow.Show();
                    
                    // Définir LoginWindow comme nouvelle MainWindow
                    System.Windows.Application.Current.MainWindow = loginWindow;
                    
                    LogMessage("LoginWindow shown");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error showing login: {ex.Message}");
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                }
            });
        }
        catch (Exception ex)
        {
            LogMessage($"Error: {ex.Message}");
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            });
        }
    }

    private Process StartApiProcess()
    {
        var desktopDir = AppDomain.CurrentDomain.BaseDirectory;

        // First try "portable"/published layouts where API is shipped next to the Desktop.
        // Supported layouts:
        // 1) Desktop.exe + DigitalisationERP.API.exe
        // 2) Desktop.exe + DigitalisationERP.API.dll
        // 3) Desktop.exe + API\DigitalisationERP.API.exe (or .dll)
        // 4) Desktop\Desktop.exe and API artifacts in parent folder
        var portableCandidates = new[]
        {
            Path.Combine(desktopDir, "DigitalisationERP.API.exe"),
            Path.Combine(desktopDir, "DigitalisationERP.API.dll"),
            Path.Combine(desktopDir, "API", "DigitalisationERP.API.exe"),
            Path.Combine(desktopDir, "API", "DigitalisationERP.API.dll"),
            Path.GetFullPath(Path.Combine(desktopDir, "..", "DigitalisationERP.API.exe")),
            Path.GetFullPath(Path.Combine(desktopDir, "..", "DigitalisationERP.API.dll")),
            Path.GetFullPath(Path.Combine(desktopDir, "..", "API", "DigitalisationERP.API.exe")),
            Path.GetFullPath(Path.Combine(desktopDir, "..", "API", "DigitalisationERP.API.dll"))
        };

        foreach (var candidate in portableCandidates)
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                var useDotnetHostPortable = candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                var startInfoPortable = new ProcessStartInfo
                {
                    FileName = useDotnetHostPortable ? "dotnet" : candidate,
                    Arguments = useDotnetHostPortable ? $"\"{candidate}\"" : string.Empty,
                    WorkingDirectory = Path.GetDirectoryName(candidate) ?? desktopDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                startInfoPortable.Environment["ASPNETCORE_URLS"] = _apiBaseUrl;
                startInfoPortable.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = _apiBaseUrl;

                var procPortable = new Process { StartInfo = startInfoPortable };
                procPortable.Start();
                LogMessage($"API process started (portable layout) with PID: {procPortable.Id} from {candidate}");
                return procPortable;
            }
            catch (Exception ex)
            {
                LogMessage($"Portable API candidate failed: {candidate} - {ex.Message}");
            }
        }

        var projectPath = FindProjectRoot(desktopDir);
        var releaseExePath = Path.Combine(projectPath, "src", "DigitalisationERP.API", "bin", "Release", "net9.0", "win-x64", "publish", "DigitalisationERP.API.exe");
        var debugDllPath = Path.Combine(projectPath, "src", "DigitalisationERP.API", "bin", "Debug", "net9.0", "DigitalisationERP.API.dll");

        string fileToRun;
        bool useDotnetHost;

        if (File.Exists(releaseExePath))
        {
            fileToRun = releaseExePath;
            useDotnetHost = false;
        }
        else if (File.Exists(debugDllPath))
        {
            fileToRun = debugDllPath;
            useDotnetHost = true;
        }
        else
        {
            var msg = $"API artifact not found. Looked for: {releaseExePath} and {debugDllPath}";
            LogMessage(msg);
            throw new FileNotFoundException(msg);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = useDotnetHost ? "dotnet" : fileToRun,
            Arguments = useDotnetHost ? $"\"{fileToRun}\"" : string.Empty,
            WorkingDirectory = Path.GetDirectoryName(fileToRun) ?? projectPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.Environment["ASPNETCORE_URLS"] = _apiBaseUrl;
        startInfo.Environment[ErpRuntimeConfig.ApiBaseUrlEnvVar] = _apiBaseUrl;

        var process = new Process { StartInfo = startInfo };

        process.Start();
        LogMessage($"API process started with PID: {process.Id}");
        return process;
    }

    private async Task<bool> WaitForApiReadyAsync(int attempts = 120, int delayMs = 500)
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

        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = await httpClient
                    .GetAsync($"{_apiBaseUrl.TrimEnd('/')}/health", HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .ConfigureAwait(false);
                LogMessage($"Health check attempt {i+1}: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    LogMessage("API health check passed!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (i % 10 == 0)
                    LogMessage($"Health check waiting... ({i+1}/120) - {ex.Message}");
            }

            await Task.Delay(delayMs).ConfigureAwait(false);
        }

        LogMessage("API health check timeout!");
        return false;
    }

    private static bool TryGetConfiguredPort(out int port)
    {
        port = 0;
        if (!Uri.TryCreate(ErpRuntimeConfig.ApiBaseUrl, UriKind.Absolute, out var uri))
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
            // If we can't determine, assume it's free and let the API start attempt decide.
        }

        return false;
    }

    private string ResolveAndSelectApiBaseUrl()
    {
        var configured = ResolveConfiguredApiBaseUrl();
        var baseUrl = NormalizeBaseUrl(configured);

        if (!TryGetPortFromUrl(baseUrl, out var port))
        {
            return ErpRuntimeConfig.DefaultApiBaseUrl;
        }

        // If something is already listening on that port, only keep it if it's our API.
        if (IsTcpPortInUse(port))
        {
            try
            {
                if (WaitForApiHealthyAsync(baseUrl, attempts: 20, delayMs: 250).GetAwaiter().GetResult())
                {
                    return baseUrl;
                }
            }
            catch
            {
                // ignore
            }

            return FindFallbackBaseUrl(baseUrl);
        }

        return baseUrl;
    }

    private static string ResolveConfiguredApiBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable(ErpRuntimeConfig.ApiBaseUrlEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
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
                    return settings.ApiBaseUrl;
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

    private static bool TryGetPortFromUrl(string baseUrl, out int port)
    {
        port = 0;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        port = uri.Port;
        return port > 0;
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

    private static string FindFallbackBaseUrl(string configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri))
        {
            return ErpRuntimeConfig.DefaultApiBaseUrl;
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

    private string FindProjectRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DigitalisationERP.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogMessage("=== App Exiting ===");
        
        // Arrêter l'API
        if (_apiProcess != null && !_apiProcess.HasExited)
        {
            try
            {
                _apiProcess.Kill();
            }
            catch { }
        }

        base.OnExit(e);
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


