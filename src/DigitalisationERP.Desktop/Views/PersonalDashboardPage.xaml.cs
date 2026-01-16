using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalisationERP.Desktop.Views
{
    public partial class PersonalDashboardPage : Page
    {
        public ObservableCollection<PersonalTask> Tasks { get; set; } = new();

        public PersonalDashboardPage()
        {
            InitializeComponent();
            LoadPersonalData();
        }

        private void LoadPersonalData()
        {
            WelcomeTitle.Text = "Mon Dashboard Personnel";
            WelcomeSubtitle.Text = "Suivez vos tâches et votre planning";

            // Sample tasks
            Tasks = new ObservableCollection<PersonalTask>
            {
                new PersonalTask 
                { 
                    Title = "Contrôle Qualité POST-02", 
                    Description = "Vérifier les 50 dernières unités produites",
                    Priority = "Haute",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    TimeEstimate = "45 min",
                    IsCompleted = true,
                    BackgroundColor = new SolidColorBrush(Color.FromRgb(232, 245, 233))
                },
                new PersonalTask 
                { 
                    Title = "Remplacement Pièces POST-05", 
                    Description = "Changement des courroies usées",
                    Priority = "Haute",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    TimeEstimate = "2h",
                    IsCompleted = false,
                    BackgroundColor = new SolidColorBrush(Color.FromRgb(255, 243, 224))
                },
                new PersonalTask 
                { 
                    Title = "Nettoyage Zone A", 
                    Description = "Nettoyage après shift précédent",
                    Priority = "Moyenne",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    TimeEstimate = "30 min",
                    IsCompleted = true,
                    BackgroundColor = new SolidColorBrush(Color.FromRgb(232, 245, 233))
                },
                new PersonalTask 
                { 
                    Title = "Formation Nouvelle Machine", 
                    Description = "Session avec le chef d'équipe",
                    Priority = "Basse",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    TimeEstimate = "1h30",
                    IsCompleted = false,
                    BackgroundColor = Brushes.White
                },
                new PersonalTask 
                { 
                    Title = "Rapport de Shift", 
                    Description = "Documenter les incidents et production",
                    Priority = "Moyenne",
                    PriorityColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    TimeEstimate = "20 min",
                    IsCompleted = false,
                    BackgroundColor = Brushes.White
                }
            };

            TasksListView.ItemsSource = Tasks;

            // Update completed tasks count
            int completed = 0;
            foreach (var task in Tasks)
            {
                if (task.IsCompleted) completed++;
            }
            MyTasksCount.Text = $"{completed}/{Tasks.Count}";
            TaskProgressBar.Value = (completed * 100.0) / Tasks.Count;
        }

        private MainWindow? GetMainWindow() => System.Windows.Application.Current?.MainWindow as MainWindow;

        private void TaskOptions_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("MyTasks");
        }

        private void RequestMaintenance_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Maintenance");
        }

        private void ReportIssue_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Maintenance");
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Email");
        }

        private void ViewSchedule_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("MySchedule");
        }
    }

    public class PersonalTask
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public SolidColorBrush PriorityColor { get; set; } = Brushes.Gray;
        public string TimeEstimate { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public SolidColorBrush BackgroundColor { get; set; } = Brushes.White;
    }
}
