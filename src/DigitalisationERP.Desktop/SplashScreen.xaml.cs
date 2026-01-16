using System.Windows;

namespace DigitalisationERP.Desktop;

/// <summary>
/// Interaction logic for SplashScreen.xaml
/// </summary>
public partial class SplashScreen : Window
{
    public SplashScreen()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // L'animation est gérée par le Storyboard dans XAML
        // La logique de transition est maintenant gérée dans App.xaml.cs
    }
}
