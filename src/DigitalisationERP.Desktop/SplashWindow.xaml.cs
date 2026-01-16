using System.Windows;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop;

public partial class SplashWindow : Window
{
    private static void Log(string msg)
    {
        try
        {
            var path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                "Projects", "ERP", "app_logs.txt");
            System.IO.File.AppendAllText(path, $"[{System.DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    public SplashWindow()
    {
        try
        {
            Log("SplashWindow.ctor - START");
            InitializeComponent();
            Log("SplashWindow.ctor - InitializeComponent done, window will display");
        }
        catch (Exception ex)
        {
            Log($"SplashWindow.ctor - ERROR: {ex.Message}");
            Log(ex.StackTrace ?? "");
            throw;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Log("Window_Loaded - Starting initialization");
        InitializeApplicationAsync();
    }

    private async void InitializeApplicationAsync()
    {
        try
        {
            Log("InitializeApplicationAsync - START");
            
            // Test backend connectivity
            Log("InitializeApplicationAsync - Creating ApiClient");
            var apiClient = new ApiClient();
            
            Log("InitializeApplicationAsync - Testing backend connection");
            // Simulate backend test (you can replace with actual API call)
            await System.Threading.Tasks.Task.Delay(1500);
            
            Log("InitializeApplicationAsync - Backend test complete");
            
            // Switch windows - Create LoginWindow on the UI thread to avoid STA errors
            this.Dispatcher.Invoke(() =>
            {
                Log("InitializeApplicationAsync - Creating LoginWindow (on UI thread)");
                var login = new LoginWindow(apiClient);
                
                Log("InitializeApplicationAsync - Setting MainWindow to LoginWindow");
                System.Windows.Application.Current.MainWindow = login;
                login.Show();
                
                Log("InitializeApplicationAsync - Closing SplashWindow");
                this.Close();
                
                Log("InitializeApplicationAsync - INITIALIZATION COMPLETE");
            });
        }
        catch (Exception ex)
        {
            Log($"InitializeApplicationAsync - ERROR: {ex.Message}");
            Log(ex.StackTrace ?? "");
            
            this.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Erreur lors de l'initialisation:\n\n{ex.Message}", "Erreur Critique");
                this.Close();
                System.Windows.Application.Current.Shutdown(1);
            });
        }
    }
}
