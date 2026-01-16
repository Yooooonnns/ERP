using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using DigitalisationERP.Core.Configuration;
using DigitalisationERP.Desktop.Services;

namespace DigitalisationERP.Desktop
{
    public partial class AdminWindow : Window
    {
        private readonly ApiClient _apiClient;
        private readonly HttpClient _httpClient;
        private readonly string _role;
        private readonly string _userId;
        private readonly ObservableCollection<PendingUserDto> _pendingUsers = new();
        private readonly ObservableCollection<string> _activityLog = new();
        private readonly List<PendingUserDto> _allPendingUsers = new();
        private int _approvedCount;
        private int _rejectedCount;

        public AdminWindow(ApiClient apiClient) : this(apiClient, role: "S_USER", userId: string.Empty)
        {
        }

        public AdminWindow(ApiClient apiClient, string role, string userId)
        {
            InitializeComponent();
            _apiClient = apiClient;
            _httpClient = new HttpClient();
            _role = string.IsNullOrWhiteSpace(role) ? "S_USER" : role;
            _userId = userId ?? string.Empty;

            // Defense-in-depth: only S_USER and Maintenance IT may access this window.
            if (!IsAllowedToManageAccounts(_role, _userId))
            {
                MessageBox.Show(
                    "Accès refusé : seul S_USER et Maintenance IT peuvent accéder à la gestion des comptes.",
                    "Accès refusé",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
                return;
            }

            UsersDataGrid.ItemsSource = _pendingUsers;
            ActivityLogListBox.ItemsSource = _activityLog;

            AddLog("Ouverture de la gestion des comptes.");
        }

        private static bool IsAllowedToManageAccounts(string role, string userId)
        {
            if (string.Equals(role, "S_USER", StringComparison.OrdinalIgnoreCase))
                return true;

            // Temporary heuristic until a dedicated Maintenance IT claim/role exists.
            return string.Equals(role, "Z_MAINT_TECH", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(userId)
                   && userId.Contains("it", StringComparison.OrdinalIgnoreCase);
        }

        private void OpenMainAppButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var permissionService = new RolePermissionService(_role, _userId);
                var mainWindow = new MainWindow(_apiClient);
                mainWindow.SetPermissionService(permissionService);
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir l'application principale.\n\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPendingUsers();
        }

        private async System.Threading.Tasks.Task LoadPendingUsers()
        {
            try
            {
                var token = _apiClient.AuthToken;
                var useAuthorized = !string.IsNullOrWhiteSpace(token) && !token.StartsWith("test_token_", StringComparison.OrdinalIgnoreCase);

                _httpClient.DefaultRequestHeaders.Clear();
                if (useAuthorized)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var endpoint = useAuthorized
                    ? $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/pending-users"
                    : $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/pending-users-public";

                var response = await _httpClient.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    // API currently returns a raw list: [{ id, email, firstName, ... }]
                    // Older/other endpoints might wrap: { data: [...] }
                    var list = JsonSerializer.Deserialize<List<PendingUserDto>>(json, options);
                    if (list == null)
                    {
                        var wrapped = JsonSerializer.Deserialize<PendingUsersResponse>(json, options);
                        list = wrapped?.Data ?? new List<PendingUserDto>();
                    }

                    _allPendingUsers.Clear();
                    _allPendingUsers.AddRange(list);

                    ApplyFiltersAndRefresh();

                    AddLog($"Utilisateurs en attente chargés: {_allPendingUsers.Count}.");
                }
                else
                {
                    MessageBox.Show("Failed to load pending users.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading pending users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFiltersAndRefresh()
        {
            var filtered = _allPendingUsers.AsEnumerable();

            var status = GetSelectedComboText(StatusFilterCombo);
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "Tous", StringComparison.OrdinalIgnoreCase))
            {
                filtered = status switch
                {
                    "En attente" => filtered.Where(u => string.IsNullOrWhiteSpace(u.Status) || u.Status.Contains("Pending", StringComparison.OrdinalIgnoreCase)),
                    "Approuvé" => filtered.Where(u => u.Status != null && u.Status.Contains("Active", StringComparison.OrdinalIgnoreCase)),
                    "Rejeté" => filtered.Where(u => u.Status != null && u.Status.Contains("Rejected", StringComparison.OrdinalIgnoreCase)),
                    _ => filtered
                };
            }

            var role = GetSelectedComboText(RoleFilterCombo);
            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, "Tous", StringComparison.OrdinalIgnoreCase))
            {
                // Pending users endpoint doesn't currently expose role; keep functional by filtering if role field is present.
                filtered = filtered.Where(u => !string.IsNullOrWhiteSpace(u.Role) && u.Role.Contains(role, StringComparison.OrdinalIgnoreCase));
            }

            _pendingUsers.Clear();
            foreach (var user in filtered)
            {
                _pendingUsers.Add(user);
            }

            // Update statistics
            TotalUsersText.Text = _allPendingUsers.Count.ToString();
            PendingUsersText.Text = _pendingUsers.Count.ToString();
            ApprovedUsersText.Text = _approvedCount.ToString();
            RejectedUsersText.Text = _rejectedCount.ToString();
        }

        private static string? GetSelectedComboText(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString();
            }

            return combo.Text;
        }

        private void AddLog(string message)
        {
            _activityLog.Insert(0, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFiltersAndRefresh();
            AddLog("Filtres appliqués.");
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadPendingUsers();
        }

        private async void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is PendingUserDto user)
            {
                await ApproveUser(user);
            }
        }

        private async void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is PendingUserDto user)
            {
                await RejectUser(user);
            }
        }

        private async void GrantSelected_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is PendingUserDto user)
            {
                await ApproveUser(user);
            }
            else
            {
                MessageBox.Show("Sélectionnez un utilisateur.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void RevokeSelected_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is PendingUserDto user)
            {
                await RejectUser(user);
            }
            else
            {
                MessageBox.Show("Sélectionnez un utilisateur.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async System.Threading.Tasks.Task ApproveUser(PendingUserDto user)
        {
            try
            {
                var token = _apiClient.AuthToken;
                var useAuthorized = !string.IsNullOrWhiteSpace(token) && !token.StartsWith("test_token_", StringComparison.OrdinalIgnoreCase);

                _httpClient.DefaultRequestHeaders.Clear();
                if (useAuthorized)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var approvalRequest = new { message = "Your account has been approved. You can now login." };
                var jsonContent = JsonSerializer.Serialize(approvalRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var endpoint = useAuthorized
                    ? $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/approve-user/{user.Id}"
                    : $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/approve-user-public/{user.Id}";

                var response = await _httpClient.PostAsync(endpoint, content);
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Account {user.Email} has been approved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    _approvedCount++;
                    _allPendingUsers.RemoveAll(u => u.Id == user.Id);
                    ApplyFiltersAndRefresh();
                    AddLog($"Compte approuvé: {user.Email}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to approve user: {response.StatusCode}\n{errorContent}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task RejectUser(PendingUserDto user)
        {
            try
            {
                var token = _apiClient.AuthToken;
                var useAuthorized = !string.IsNullOrWhiteSpace(token) && !token.StartsWith("test_token_", StringComparison.OrdinalIgnoreCase);

                _httpClient.DefaultRequestHeaders.Clear();
                if (useAuthorized)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var rejectionRequest = new { message = "Your account registration has been rejected." };
                var jsonContent = JsonSerializer.Serialize(rejectionRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var endpoint = useAuthorized
                    ? $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/reject-user/{user.Id}"
                    : $"{ErpRuntimeConfig.ApiBaseUrl}/api/auth/reject-user-public/{user.Id}";

                var response = await _httpClient.PostAsync(endpoint, content);
                
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Account {user.Email} has been rejected.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    _rejectedCount++;
                    _allPendingUsers.RemoveAll(u => u.Id == user.Id);
                    ApplyFiltersAndRefresh();
                    AddLog($"Compte rejeté: {user.Email}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to reject user: {response.StatusCode}\n{errorContent}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rejecting user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PendingUserDto
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public string Department { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class PendingUsersResponse
    {
        public List<PendingUserDto> Data { get; set; } = new();
    }
}
