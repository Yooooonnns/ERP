using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DigitalisationERP.Application.Services;
using DigitalisationERP.Core.Entities;

namespace DigitalisationERP.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel pour le Dashboard de Maintenance Avancé
    /// Gère la logique et l'état de la page Maintenance
    /// </summary>
    public class MaintenanceDashboardViewModel : INotifyPropertyChanged
    {
        private readonly MaintenanceHealthScoreCalculationService _healthScoreService;
        private readonly MaintenanceAlertManager _alertManager;
        private readonly PlannedMaintenanceService _plannedMaintenanceService;

        // Propriétés UI
        private ObservableCollection<MaintenanceAlertViewModel> _activeAlerts;
        private ObservableCollection<PlannedMaintenanceViewModel> _plannedTasks;
        private ObservableCollection<PostHealthScoreViewModel> _postHealthScores;
        private MaintenanceKPIsViewModel _kpis;
        private string _selectedLineId;
        private bool _isLoading;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MaintenanceDashboardViewModel()
        {
            _healthScoreService = new MaintenanceHealthScoreCalculationService();
            _alertManager = new MaintenanceAlertManager(_healthScoreService);
            _plannedMaintenanceService = new PlannedMaintenanceService(new List<MaintenanceSchedule>());

            _activeAlerts = new ObservableCollection<MaintenanceAlertViewModel>();
            _plannedTasks = new ObservableCollection<PlannedMaintenanceViewModel>();
            _postHealthScores = new ObservableCollection<PostHealthScoreViewModel>();
            _kpis = new MaintenanceKPIsViewModel();
            _selectedLineId = "All";

            // Commandes
            RefreshCommand = new RelayCommand(_ => RefreshData());
            AddMaintenanceCommand = new RelayCommand(_ => AddMaintenanceTask());
            AcknowledgeAlertCommand = new RelayCommand(alert =>
            {
                if (alert is MaintenanceAlertViewModel vm)
                {
                    AcknowledgeAlert(vm);
                }
            });
        }

        #region Propriétés

        public ObservableCollection<MaintenanceAlertViewModel> ActiveAlerts
        {
            get => _activeAlerts;
            set
            {
                if (_activeAlerts != value)
                {
                    _activeAlerts = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<PlannedMaintenanceViewModel> PlannedTasks
        {
            get => _plannedTasks;
            set
            {
                if (_plannedTasks != value)
                {
                    _plannedTasks = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<PostHealthScoreViewModel> PostHealthScores
        {
            get => _postHealthScores;
            set
            {
                if (_postHealthScores != value)
                {
                    _postHealthScores = value;
                    OnPropertyChanged();
                }
            }
        }

        public MaintenanceKPIsViewModel KPIs
        {
            get => _kpis;
            set
            {
                if (_kpis != value)
                {
                    _kpis = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedLineId
        {
            get => _selectedLineId;
            set
            {
                if (_selectedLineId != value)
                {
                    _selectedLineId = value;
                    OnPropertyChanged();
                    RefreshData();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commandes

        public ICommand RefreshCommand { get; }
        public ICommand AddMaintenanceCommand { get; }
        public ICommand AcknowledgeAlertCommand { get; }

        #endregion

        #region Méthodes Métier

        public void RefreshData()
        {
            IsLoading = true;

            try
            {
                // Rafraîchir les alertes
                UpdateAlerts();

                // Rafraîchir les health scores
                UpdateHealthScores();

                // Rafraîchir les tâches planifiées
                UpdatePlannedTasks();

                // Rafraîchir les KPIs
                UpdateKPIs();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateAlerts()
        {
            ActiveAlerts.Clear();

            // Récupère les alertes du gestionnaire
            var alerts = _alertManager.GetActiveAlerts();

            // Filtre par ligne si sélectionnée
            if (!string.IsNullOrEmpty(SelectedLineId) && SelectedLineId != "All")
            {
                // Appliquer le filtre
            }

            // Convertit en ViewModels
            foreach (var alert in alerts.OrderByDescending(a => a.CreatedAt).Take(15))
            {
                ActiveAlerts.Add(new MaintenanceAlertViewModel
                {
                    AlertId = alert.AlertId,
                    PostCode = alert.PostCode,
                    PostName = alert.PostName,
                    Title = alert.Title,
                    Description = alert.Description,
                    Severity = alert.Severity,
                    Icon = alert.Icon,
                    CreatedAt = alert.CreatedAt,
                    DueDate = alert.DueDate,
                    Status = alert.Status,
                    SeverityColor = alert.Severity switch
                    {
                        "Critical" => "#FF5252",
                        "High" => "#FF9800",
                        "Medium" => "#FFC107",
                        _ => "#4CAF50"
                    }
                });
            }
        }

        private void UpdateHealthScores()
        {
            PostHealthScores.Clear();

            // Mock data pour démonstration
            var scores = new Dictionary<string, double>
            {
                { "POST-A01", 92.5 },
                { "POST-A02", 78.3 },
                { "POST-A03", 45.2 },
                { "POST-A04", 88.9 }
            };

            foreach (var (postCode, score) in scores)
            {
                PostHealthScores.Add(new PostHealthScoreViewModel
                {
                    PostCode = postCode,
                    HealthScore = score,
                    Status = score >= 85 ? "Good" : score >= 70 ? "Warning" : score >= 50 ? "Scheduled" : "Critical",
                    Icon = score >= 85 ? "🟢" : score >= 70 ? "🟡" : score >= 50 ? "🟠" : "🔴",
                    LastMaintenanceDate = DateTime.Now.AddDays(-7),
                    NextScheduledMaintenance = DateTime.Now.AddDays(14)
                });
            }
        }

        private void UpdatePlannedTasks()
        {
            PlannedTasks.Clear();

            // Mock data
            var tasks = new List<(string PostCode, string Title, DateTime Start, string Status)>
            {
                ("POST-A01", "Oil change and filter replacement", DateTime.Now.AddDays(2), "Scheduled"),
                ("POST-A03", "Sensor calibration", DateTime.Now.AddDays(1), "Scheduled"),
                ("POST-A02", "Belt inspection", DateTime.Now.AddDays(5), "Scheduled")
            };

            foreach (var (postCode, title, startDate, status) in tasks)
            {
                PlannedTasks.Add(new PlannedMaintenanceViewModel
                {
                    PostCode = postCode,
                    Title = title,
                    ScheduledStartDate = startDate,
                    Status = status,
                    DaysUntilDue = (int)(startDate - DateTime.Now).TotalDays
                });
            }
        }

        private void UpdateKPIs()
        {
            // Mock KPIs
            KPIs = new MaintenanceKPIsViewModel
            {
                MTBF = 45.2,
                MTTR = 2.5,
                Availability = 94.2,
                OverdueTasksRate = 8.3,
                TotalScheduledTasks = 12,
                CompletedTasks = 48,
                OverdueTasks = 3,
                InProgressTasks = 2,
                AverageHealthScore = 76.2
            };
        }

        private void AddMaintenanceTask()
        {
            // Ouvrir dialog pour ajouter maintenance
            System.Windows.MessageBox.Show("Add maintenance dialog would open here");
        }

        private void AcknowledgeAlert(MaintenanceAlertViewModel alert)
        {
            if (alert == null) return;

            _alertManager.AcknowledgeAlert(alert.AlertId);
            ActiveAlerts.Remove(alert);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
        }

        #endregion
    }

    #region ViewModels Imbriquées

    public class MaintenanceAlertViewModel
    {
        public string AlertId { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;
        public string PostName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string SeverityColor { get; set; } = string.Empty;
    }

    public class PostHealthScoreViewModel
    {
        public string PostCode { get; set; } = string.Empty;
        public double HealthScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public DateTime LastMaintenanceDate { get; set; }
        public DateTime NextScheduledMaintenance { get; set; }
    }

    public class PlannedMaintenanceViewModel
    {
        public string PostCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ScheduledStartDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int DaysUntilDue { get; set; }
    }

    public class MaintenanceKPIsViewModel
    {
        public double MTBF { get; set; }
        public double MTTR { get; set; }
        public double Availability { get; set; }
        public double OverdueTasksRate { get; set; }
        public int TotalScheduledTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int InProgressTasks { get; set; }
        public double AverageHealthScore { get; set; }
    }

    #endregion

    #region Utilitaires

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add
            {
                if (value != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove
            {
                if (value != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }

    #endregion
}
