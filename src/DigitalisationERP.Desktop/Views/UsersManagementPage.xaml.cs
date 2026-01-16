using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalisationERP.Desktop.Services;
using Microsoft.Win32;

namespace DigitalisationERP.Desktop.Views
{
    public partial class UsersManagementPage : Page
    {
        private readonly RolePermissionService? _permissionService;
        private readonly ApiClient? _apiClient;
        private readonly ApiService? _apiService;

        public ObservableCollection<UserItem> Users { get; set; } = new();

        public UsersManagementPage(RolePermissionService? permissionService, ApiClient? apiClient)
        {
            InitializeComponent();
            _permissionService = permissionService;
            _apiClient = apiClient;

            if (_apiClient != null)
            {
                _apiService = new ApiService();
                if (!string.IsNullOrWhiteSpace(_apiClient.AuthToken))
                {
                    _apiService.SetAccessToken(_apiClient.AuthToken);
                }
            }

            Loaded += async (_, __) => await LoadUsersAsync();
        }

        private bool CanAdminUsers()
        {
            if (_permissionService == null) return false;
            return _permissionService.CurrentRole == RolePermissionService.UserRole.S_USER
                   || _permissionService.CurrentRole == RolePermissionService.UserRole.Z_MAINT_TECH
                   || _permissionService.CurrentRole == RolePermissionService.UserRole.Z_MAINT_MANAGER
                   || _permissionService.CurrentRole == RolePermissionService.UserRole.Z_MAINT_PLANNER;
        }

        private static string FormatRelative(DateTime? utc)
        {
            if (!utc.HasValue) return "—";
            var dt = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime();
            var span = DateTime.Now - dt;
            if (span.TotalMinutes < 1) return "À l'instant";
            if (span.TotalMinutes < 60) return $"Il y a {(int)span.TotalMinutes} min";
            if (span.TotalHours < 24) return $"Il y a {(int)span.TotalHours} h";
            if (span.TotalDays < 7) return $"Il y a {(int)span.TotalDays} jours";
            return dt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
        }

        private static SolidColorBrush PickAvatarBrush(string key)
        {
            // Deterministic palette
            var colors = new[]
            {
                Color.FromRgb(33,150,243),
                Color.FromRgb(233,30,99),
                Color.FromRgb(0,188,212),
                Color.FromRgb(255,152,0),
                Color.FromRgb(76,175,80),
                Color.FromRgb(103,58,183),
                Color.FromRgb(121,85,72),
                Color.FromRgb(96,125,139),
            };
            var hash = key.GetHashCode();
            var idx = Math.Abs(hash) % colors.Length;
            return new SolidColorBrush(colors[idx]);
        }

        private static SolidColorBrush RoleBrush(string role)
        {
            return role switch
            {
                "S_USER" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                "SAP_ALL" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                _ when role.StartsWith("Z_PROD", StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                _ when role.StartsWith("Z_MAINT", StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                _ when role.StartsWith("Z_WM", StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(121, 85, 72)),
                _ when role.StartsWith("Z_QM", StringComparison.OrdinalIgnoreCase) => new SolidColorBrush(Color.FromRgb(0, 188, 212)),
                _ => new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            };
        }

        private static string InitialsFrom(string fullName)
        {
            var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
        }

        private async Task LoadUsersAsync()
        {
            if (_apiClient == null)
            {
                UsersDataGrid.ItemsSource = Users;
                return;
            }

            try
            {
                var users = await _apiClient.GetUsersAsync(1, 200);
                Users.Clear();

                foreach (var u in users)
                {
                    var fullName = $"{u.firstName} {u.lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(fullName)) fullName = u.username ?? u.email ?? "(unknown)";

                    var primaryRole = (u.roles != null && u.roles.Count > 0) ? u.roles[0] : "";
                    var isActive = u.isActive;
                    var statusText = isActive ? "Actif" : "Inactif";
                    var statusColor = isActive
                        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                        : new SolidColorBrush(Color.FromRgb(158, 158, 158));

                    Users.Add(new UserItem
                    {
                        UserId = u.userId,
                        Username = u.username ?? string.Empty,
                        FirstName = u.firstName ?? string.Empty,
                        LastName = u.lastName ?? string.Empty,
                        PhoneNumber = u.phoneNumber,
                        FullName = fullName,
                        Email = u.email ?? string.Empty,
                        Initials = InitialsFrom(fullName),
                        AvatarColor = PickAvatarBrush(u.email ?? u.username ?? fullName),
                        Role = primaryRole,
                        RoleColor = RoleBrush(primaryRole),
                        Department = u.department ?? string.Empty,
                        LastLogin = FormatRelative(u.lastLoginDate),
                        Status = statusText,
                        StatusColor = statusColor
                    });
                }

                UsersDataGrid.ItemsSource = Users;
                UsersDataGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAdminUsers())
            {
                MessageBox.Show("Vous n'avez pas les droits pour créer des utilisateurs.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_apiService == null)
            {
                MessageBox.Show("API not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new UserEditDialog("Créer un utilisateur");
            if (dialog.ShowDialog() != true) return;

            _ = CreateUserAsync(dialog);
        }

        private async Task CreateUserAsync(UserEditDialog dialog)
        {
            try
            {
                if (_apiService == null) return;

                var body = new
                {
                    username = dialog.Username,
                    email = dialog.Email,
                    password = dialog.Password,
                    firstName = dialog.FirstName,
                    lastName = dialog.LastName,
                    phoneNumber = dialog.PhoneNumber,
                    department = dialog.Department,
                    roles = dialog.Roles
                };

                var resp = await _apiService.PostAsync<object>("/api/users", body);
                if (!resp.Success)
                {
                    MessageBox.Show(resp.Errors.FirstOrDefault() ?? resp.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Create user failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"users_{DateTime.Now:yyyy-MM-dd}.csv",
                    Title = "Export users"
                };

                if (saveDialog.ShowDialog() != true) return;

                using var sw = new StreamWriter(saveDialog.FileName);
                sw.WriteLine("UserId;FullName;Email;Role;Department;LastLogin;Status");
                foreach (var u in Users)
                {
                    sw.WriteLine($"{u.UserId};{Escape(u.FullName)};{Escape(u.Email)};{Escape(u.Role)};{Escape(u.Department)};{Escape(u.LastLogin)};{Escape(u.Status)}");
                }

                MessageBox.Show("Export completed.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Escape(string? s)
        {
            s ??= string.Empty;
            return s.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as FrameworkElement)?.DataContext as UserItem;
            if (user == null) return;

            if (!CanAdminUsers())
            {
                MessageBox.Show("Vous n'avez pas les droits pour modifier des utilisateurs.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new UserEditDialog("Modifier l'utilisateur")
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Department = user.Department,
                PhoneNumber = user.PhoneNumber ?? string.Empty
            };
            if (dialog.ShowDialog() != true) return;
            _ = UpdateUserAsync(dialog);
        }

        private async Task UpdateUserAsync(UserEditDialog dialog)
        {
            try
            {
                if (_apiService == null) return;

                var body = new
                {
                    username = dialog.Username,
                    email = dialog.Email,
                    firstName = dialog.FirstName,
                    lastName = dialog.LastName,
                    phoneNumber = dialog.PhoneNumber,
                    department = dialog.Department
                };
                var resp = await _apiService.PutAsync<object, object>($"/api/users/{dialog.UserId}", body);
                if (!resp.Success)
                {
                    MessageBox.Show(resp.Errors.FirstOrDefault() ?? resp.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (dialog.Roles.Count > 0)
                {
                    var rolesResp = await _apiService.PostAsync<object>($"/api/users/{dialog.UserId}/roles", new { roles = dialog.Roles });
                    if (!rolesResp.Success)
                    {
                        MessageBox.Show(rolesResp.Errors.FirstOrDefault() ?? rolesResp.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update user failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleUserStatus_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as FrameworkElement)?.DataContext as UserItem;
            if (user == null) return;

            if (!CanAdminUsers())
            {
                MessageBox.Show("Vous n'avez pas les droits pour modifier le statut.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _ = ToggleUserActiveAsync(user);
        }

        private async Task ToggleUserActiveAsync(UserItem user)
        {
            try
            {
                if (_apiService == null) return;

                var newIsActive = user.Status != "Actif";
                var resp = await _apiService.PutAsync<object, object>($"/api/users/{user.UserId}", new { isActive = newIsActive });
                if (!resp.Success)
                {
                    MessageBox.Show(resp.Errors.FirstOrDefault() ?? resp.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var user = (sender as FrameworkElement)?.DataContext as UserItem;
            if (user == null) return;

            if (!CanAdminUsers())
            {
                MessageBox.Show("Vous n'avez pas les droits pour supprimer des utilisateurs.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Delete user {user.FullName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _ = DeleteUserAsync(user);
        }

        private async Task DeleteUserAsync(UserItem user)
        {
            try
            {
                if (_apiClient == null) return;
                await _apiClient.DeleteUserAsync(user.UserId);
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UserItem
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public SolidColorBrush AvatarColor { get; set; } = Brushes.Gray;
        public string Role { get; set; } = string.Empty;
        public SolidColorBrush RoleColor { get; set; } = Brushes.Gray;
        public string Department { get; set; } = string.Empty;
        public string LastLogin { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public SolidColorBrush StatusColor { get; set; } = Brushes.Gray;
    }

    internal sealed class UserEditDialog : Window
    {
        private readonly TextBox _username;
        private readonly TextBox _email;
        private readonly PasswordBox _password;
        private readonly TextBox _firstName;
        private readonly TextBox _lastName;
        private readonly TextBox _department;
        private readonly TextBox _phone;
        private readonly TextBox _roles;

        public long UserId { get; set; }
        public string Username { get => _username.Text; set => _username.Text = value; }
        public string Email { get => _email.Text; set => _email.Text = value; }
        public string Password { get => _password.Password; set => _password.Password = value; }
        public string FirstName { get => _firstName.Text; set => _firstName.Text = value; }
        public string LastName { get => _lastName.Text; set => _lastName.Text = value; }
        public string Department { get => _department.Text; set => _department.Text = value; }
        public string PhoneNumber { get => _phone.Text; set => _phone.Text = value; }
        public List<string> Roles
        {
            get
            {
                return _roles.Text
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            set => _roles.Text = string.Join(", ", value);
        }

        public UserEditDialog(string title)
        {
            Title = title;
            Width = 520;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(16) };

            _username = AddField(panel, "Username");
            _email = AddField(panel, "Email");
            _password = new PasswordBox { Margin = new Thickness(0, 4, 0, 12) };
            panel.Children.Add(new TextBlock { Text = "Password (required for create)", FontWeight = FontWeights.SemiBold });
            panel.Children.Add(_password);

            _firstName = AddField(panel, "First name");
            _lastName = AddField(panel, "Last name");
            _department = AddField(panel, "Department");
            _phone = AddField(panel, "Phone number");
            _roles = AddField(panel, "Roles (comma-separated, e.g. S_USER, Z_PROD_MANAGER)");

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancel", Margin = new Thickness(0, 8, 8, 0), MinWidth = 90 };
            cancel.Click += (_, __) => { DialogResult = false; Close(); };
            var ok = new Button { Content = "OK", Margin = new Thickness(0, 8, 0, 0), MinWidth = 90 };
            ok.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email))
                {
                    MessageBox.Show("Username and Email are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
                Close();
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            panel.Children.Add(buttons);

            Content = panel;
        }

        private static TextBox AddField(Panel panel, string label)
        {
            panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
            var tb = new TextBox { Margin = new Thickness(0, 4, 0, 12) };
            panel.Children.Add(tb);
            return tb;
        }
    }
}
