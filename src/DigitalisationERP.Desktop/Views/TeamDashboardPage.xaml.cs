using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DigitalisationERP.Desktop.Views
{
    public partial class TeamDashboardPage : Page
    {
        public ObservableCollection<TeamMember> TeamMembers { get; set; } = new();
        public ObservableCollection<TaskItem> Tasks { get; set; } = new();

        public TeamDashboardPage()
        {
            InitializeComponent();
            LoadTeamData();
            DataContext = this;
        }

        private void LoadTeamData()
        {
            // Sample team members
            TeamMembers = new ObservableCollection<TeamMember>
            {
                new TeamMember { Name = "Marc Dupont", Role = "Chef d'Équipe", Shift = "Matin", ActiveTasks = 5, Status = "Actif", StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)) },
                new TeamMember { Name = "Sophie Martin", Role = "Opérateur", Shift = "Matin", ActiveTasks = 3, Status = "Actif", StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)) },
                new TeamMember { Name = "Pierre Durand", Role = "Opérateur", Shift = "Après-midi", ActiveTasks = 4, Status = "Actif", StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)) },
                new TeamMember { Name = "Julie Bernard", Role = "Magasinier", Shift = "Matin", ActiveTasks = 2, Status = "Actif", StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)) },
                new TeamMember { Name = "Thomas Laurent", Role = "Opérateur", Shift = "Nuit", ActiveTasks = 0, Status = "Hors ligne", StatusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158)) },
                new TeamMember { Name = "Emma Petit", Role = "Chef d'Équipe", Shift = "Après-midi", ActiveTasks = 6, Status = "Actif", StatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)) }
            };
            TeamMembersGrid.ItemsSource = TeamMembers;

            // Sample tasks
            Tasks = new ObservableCollection<TaskItem>
            {
                new TaskItem { TaskName = "Maintenance POST-03", AssignedTo = "Marc Dupont", Priority = "Haute", Deadline = "Aujourd'hui", Status = "En cours", StatusColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)) },
                new TaskItem { TaskName = "Contrôle Qualité Lot-145", AssignedTo = "Sophie Martin", Priority = "Moyenne", Deadline = "16 Jan", Status = "En cours", StatusColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)) },
                new TaskItem { TaskName = "Réappro Pièces POST-05", AssignedTo = "Julie Bernard", Priority = "Haute", Deadline = "15 Jan", Status = "En cours", StatusColor = new SolidColorBrush(Color.FromRgb(255, 152, 0)) },
                new TaskItem { TaskName = "Formation Nouvelle Machine", AssignedTo = "Pierre Durand", Priority = "Basse", Deadline = "20 Jan", Status = "Planifiée", StatusColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)) },
                new TaskItem { TaskName = "Rapport Hebdomadaire", AssignedTo = "Emma Petit", Priority = "Moyenne", Deadline = "17 Jan", Status = "À faire", StatusColor = new SolidColorBrush(Color.FromRgb(158, 158, 158)) },
                new TaskItem { TaskName = "Nettoyage Zone A", AssignedTo = "Non assigné", Priority = "Basse", Deadline = "18 Jan", Status = "Non assignée", StatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)) }
            };
            TasksGrid.ItemsSource = Tasks;
        }

        private MainWindow? GetMainWindow() => System.Windows.Application.Current?.MainWindow as MainWindow;

        private void PlanShift_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("MySchedule");
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("Email");
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                _ = mainWindow.ExportReportAsync();
            }
        }
        
        private void AddMember_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("UsersManagement");
        }
        
        private void NewTask_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("MyTasks");
        }
        
        private void ApproveLeave_Click(object sender, RoutedEventArgs e)
        {
            RemoveNotificationCard(sender);
            MessageBox.Show("Demande approuvée.", "Confirmation", MessageBoxButton.OK, MessageBoxImage.Information);
            GetMainWindow()?.NavigateToPage("MySchedule");
        }
        
        private void RejectLeave_Click(object sender, RoutedEventArgs e)
        {
            RemoveNotificationCard(sender);
            MessageBox.Show("Demande refusée.", "Confirmation", MessageBoxButton.OK, MessageBoxImage.Information);
            GetMainWindow()?.NavigateToPage("MySchedule");
        }
        
        private void ViewAllNotifications_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.NavigateToPage("MyTasks");
        }

        private void RemoveNotificationCard(object sender)
        {
            if (sender is not DependencyObject dep)
            {
                return;
            }

            var parent = dep;
            while (parent != null)
            {
                if (parent is Border border && NotificationsPanel.Children.Contains(border))
                {
                    NotificationsPanel.Children.Remove(border);
                    return;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }
    }

    public class TeamMember
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Shift { get; set; } = string.Empty;
        public int ActiveTasks { get; set; }
        public string Status { get; set; } = string.Empty;
        public SolidColorBrush StatusColor { get; set; } = Brushes.Gray;
    }

    public class TaskItem
    {
        public string TaskName { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Deadline { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public SolidColorBrush StatusColor { get; set; } = Brushes.Gray;
    }
}
