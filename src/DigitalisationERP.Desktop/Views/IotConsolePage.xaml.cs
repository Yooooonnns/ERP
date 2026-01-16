using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Services.IoT;
using DigitalisationERP.Desktop.Models;
using Microsoft.Win32;

namespace DigitalisationERP.Desktop.Views
{
    public partial class IotConsolePage : UserControl
    {
        private IotSimulationService? _iotSimulation;
        private IIotProvider? _iotProvider;
        private LocalLogService? _logService;

        public IotConsolePage()
        {
            InitializeComponent();
            Loaded += IotConsolePage_Loaded;
        }

        private void IotConsolePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Récupérer les services depuis MainWindow
            var mainWindow = Window.GetWindow(this) as MainWindow;
            _iotProvider = mainWindow?.GetIotProvider();
            _iotSimulation = mainWindow?.GetIotSimulation();
            _logService = mainWindow?.GetLogService();

            if (_iotSimulation != null)
            {
                // Bind les collections aux contrôles (legacy pour compatibilité)
                LogsListBox.ItemsSource = _iotSimulation.LogEvents;
                SensorsListBox.ItemsSource = _iotSimulation.SensorReadings;
                RobotsListBox.ItemsSource = _iotSimulation.Robots;
            }
        }

        private async void ExportLogsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_logService == null)
            {
                MessageBox.Show("Service de logs non initialisé", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Dialogue de sauvegarde
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Fichiers CSV (*.csv)|*.csv",
                    FileName = $"iot_logs_{DateTime.Now:yyyy-MM-dd}.csv",
                    Title = "Exporter les logs IoT"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Exporter les logs des 7 derniers jours
                    var endDate = DateTime.Now;
                    var startDate = endDate.AddDays(-7);
                    
                    await _logService.ExportLogsAsync(startDate, endDate, saveDialog.FileName);
                    
                    MessageBox.Show(
                        $"Logs exportés avec succès !\n\nFichier : {saveDialog.FileName}\nPériode : {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                        "Export réussi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erreur lors de l'export des logs :\n{ex.Message}",
                    "Erreur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SimulateFailureButton_Click(object sender, RoutedEventArgs e)
        {
            if (_iotSimulation == null) return;

            // Simuler une panne sur POST-03
            _iotSimulation.SimulatePostFailure("POST-03");

            // Le robot de maintenance sera envoyé automatiquement grâce à l'événement CriticalAlertRaised
            MessageBox.Show(
                "Panne simulée sur POST-03\nLe robot de maintenance a été alerté automatiquement.",
                "Simulation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void SendSupplyRobotButton_Click(object sender, RoutedEventArgs e)
        {
            if (_iotProvider == null)
            {
                MessageBox.Show("Provider IoT non initialisé", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Demander confirmation avant l'envoi du robot (Safety Feature)
            bool confirmed = ConfirmationDialog.Show(
                "⚠️ ENVOI ROBOT - CONFIRMATION",
                "Vous êtes sur le point d'envoyer un robot vers POST-02.",
                "Assurez-vous que la zone est sécurisée et qu'aucun personnel n'est présent sur la trajectoire du robot.",
                Window.GetWindow(this)
            );

            if (!confirmed)
            {
                return;
            }

            // Utiliser le HAL pour envoyer le robot
            var robots = await _iotProvider.GetAllRobotsAsync();
            var supplyRobot = robots.FirstOrDefault(r => r.Type == RobotType.SupplyChain)
                           ?? robots.FirstOrDefault(r => r.RobotId.Contains("SUPPLY", StringComparison.OrdinalIgnoreCase))
                           ?? robots.FirstOrDefault();
            
            if (supplyRobot != null)
            {
                var success = await _iotProvider.SendRobotCommandAsync(supplyRobot.RobotId, RobotCommand.GoToLocation, "POST-02");
                
                if (success)
                {
                    // Enregistrer la commande dans les logs
                    if (_logService != null)
                    {
                        await _logService.LogRobotCommandAsync(supplyRobot.RobotId, "GoToLocation", "POST-02");
                    }

                    MessageBox.Show(
                        $"Robot {supplyRobot.RobotId} envoyé vers POST-02\nSuivez les logs en temps réel.",
                        "Commande Robot",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Aucun robot disponible.", "Commande Robot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
