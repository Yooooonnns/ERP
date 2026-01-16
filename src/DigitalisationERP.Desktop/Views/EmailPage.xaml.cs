using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DigitalisationERP.Desktop.Models;
using DigitalisationERP.Desktop.Services;
using DigitalisationERP.Desktop.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace DigitalisationERP.Desktop.Views;

public partial class EmailPage : UserControl
{
    private readonly LoginResponse _loginResponse;
    private readonly ApiService _apiService;
    private List<WorkerItem> _allWorkers = new();
    private string _currentFolder = "Inbox";
    private readonly List<InternalEmailAttachmentDto> _pendingAttachments = new();

    public EmailPage(LoginResponse loginResponse)
    {
        InitializeComponent();
        _loginResponse = loginResponse;
        _apiService = new ApiService();

        if (!string.IsNullOrWhiteSpace(loginResponse.AccessToken))
        {
            _apiService.SetAccessToken(loginResponse.AccessToken);
        }

        Loaded += async (_, _) =>
        {
            await InitializeAsync();
        };
    }

    private async Task InitializeAsync()
    {
        await LoadWorkersAsync();
        await LoadEmailsAsync("Inbox");
    }

    private async Task LoadWorkersAsync()
    {
        try
        {
            var response = await _apiService.GetAsync<List<WorkerItem>>("/api/InternalEmail/workers");
            if (response.Success && response.Data != null)
            {
                _allWorkers = response.Data;
                WorkersListBox.ItemsSource = _allWorkers;
                return;
            }
        }
        catch { }
        
        // Fallback to mock data if API fails
        var workers = new[]
        {
            new { Name = "John Smith", Role = "Manager", Initials = "JS", Email = "john.smith@company.com" },
            new { Name = "Sarah Johnson", Role = "Production Lead", Initials = "SJ", Email = "sarah.j@company.com" },
            new { Name = "Mike Wilson", Role = "Inventory Manager", Initials = "MW", Email = "mike.w@company.com" },
            new { Name = "Emily Davis", Role = "Sales Director", Initials = "ED", Email = "emily.d@company.com" },
            new { Name = "Robert Brown", Role = "HR Manager", Initials = "RB", Email = "robert.b@company.com" },
            new { Name = "Lisa Garcia", Role = "Finance Manager", Initials = "LG", Email = "lisa.g@company.com" },
            new { Name = "David Martinez", Role = "IT Support", Initials = "DM", Email = "david.m@company.com" },
            new { Name = "Jennifer Taylor", Role = "Quality Control", Initials = "JT", Email = "jennifer.t@company.com" }
        };

        WorkersListBox.ItemsSource = workers;
    }

    private async Task LoadEmailsAsync(string folder)
    {
        _currentFolder = folder;
        FolderTitleText.Text = folder;

        if (folder == "Reports")
        {
            // Simple report templates (generation/attachment happens from compose for now)
            var reports = new[]
            {
                new EmailItem
                {
                    Sender = "System - Daily Report",
                    SenderInitials = "SR",
                    Subject = "Daily Production Report",
                    Preview = "Generate a production report file (txt/json) and attach it to a message.",
                    Time = "",
                    HasAttachment = true
                },
                new EmailItem
                {
                    Sender = "System - Maintenance Report",
                    SenderInitials = "MR",
                    Subject = "Maintenance Intervention Report",
                    Preview = "Fill and send an intervention report (manual for now; autofill will come from intervention completion).",
                    Time = "",
                    HasAttachment = true
                }
            };
            EmailListBox.ItemsSource = reports;
            InboxCount.Text = "";
            InboxCount.Visibility = Visibility.Collapsed;
        }
        else if (folder == "Inbox")
        {
            // Prefer real API data when authenticated; fallback to mock data.
            try
            {
                if (_apiService.IsAuthenticated)
                {
                    var endpoint = "/api/InternalEmail/inbox";
                    var response = await _apiService.GetAsync<InternalEmailListResponseDto>(endpoint);
                    if (response.Success && response.Data != null)
                    {
                        var items = response.Data.Emails
                            .Select(e => new EmailItem
                            {
                                Id = e.Id,
                                Sender = e.SenderName,
                                SenderInitials = string.IsNullOrWhiteSpace(e.SenderInitials) ? GetInitialsFallback(e.SenderName) : e.SenderInitials,
                                Subject = e.Subject,
                                Preview = string.IsNullOrWhiteSpace(e.Preview) ? Truncate(e.Body, 120) : e.Preview,
                                Time = string.IsNullOrWhiteSpace(e.Time) ? e.SentAt.ToLocalTime().ToString("g") : e.Time,
                                HasAttachment = e.HasAttachment,
                                Body = e.Body,
                                IsRead = e.IsRead
                            })
                            .ToList();

                        EmailListBox.ItemsSource = items;

                        var unread = response.Data.UnreadCount;
                        InboxCount.Text = unread > 0 ? unread.ToString() : string.Empty;
                        InboxCount.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;

                        return;
                    }
                }
            }
            catch
            {
                // swallow and fallback to mock below
            }

            var emails = new[]
            {
                new EmailItem
                {
                    Sender = "Sarah Johnson",
                    SenderInitials = "SJ",
                    Subject = "Production Line 3 Status Update",
                    Preview = "The maintenance work on Line 3 has been completed. We can resume...",
                    Time = "10:30 AM",
                    HasAttachment = false,
                    Body = "The maintenance work on Line 3 has been completed. We can resume production.",
                    IsRead = false
                },
                new EmailItem
                {
                    Sender = "Mike Wilson",
                    SenderInitials = "MW",
                    Subject = "Inventory Restock Required",
                    Preview = "We need to reorder the following items ASAP: Raw Material A...",
                    Time = "09:15 AM",
                    HasAttachment = true,
                    Body = "We need to reorder the following items ASAP: Raw Material A...",
                    IsRead = true
                }
            };
            EmailListBox.ItemsSource = emails;

            InboxCount.Text = string.Empty;
            InboxCount.Visibility = Visibility.Collapsed;
        }
        else if (folder == "Sent")
        {
            // Sent button becomes a chat/messages entry: show one-to-one threads.
            FolderTitleText.Text = "Messages";

            try
            {
                if (_apiService.IsAuthenticated)
                {
                    var response = await _apiService.GetAsync<List<InternalThreadDto>>("/api/InternalEmail/threads");
                    if (response.Success && response.Data != null)
                    {
                        var items = response.Data.Select(t => new EmailItem
                        {
                            ThreadUserId = t.OtherUserId,
                            Sender = t.OtherUserName,
                            SenderInitials = string.IsNullOrWhiteSpace(t.OtherUserInitials) ? GetInitialsFallback(t.OtherUserName) : t.OtherUserInitials,
                            Subject = string.IsNullOrWhiteSpace(t.LastSubject) ? "Message" : t.LastSubject,
                            Preview = t.LastPreview,
                            Time = string.IsNullOrWhiteSpace(t.LastTime) ? t.LastSentAt.ToLocalTime().ToString("g") : t.LastTime,
                            HasAttachment = false,
                            Body = string.Empty,
                            IsRead = t.UnreadCount == 0
                        }).ToList();

                        EmailListBox.ItemsSource = items;
                        InboxCount.Text = string.Empty;
                        InboxCount.Visibility = Visibility.Collapsed;
                        return;
                    }
                }
            }
            catch
            {
                // ignore
            }

            EmailListBox.ItemsSource = Array.Empty<EmailItem>();
            InboxCount.Text = string.Empty;
            InboxCount.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Empty for other folders
            EmailListBox.ItemsSource = null;
            InboxCount.Text = string.Empty;
            InboxCount.Visibility = Visibility.Collapsed;
        }
    }

    private void ComposeButton_Click(object sender, RoutedEventArgs e)
    {
        ShowComposeView();
    }

    private async void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string folder)
        {
            try
            {
                await LoadEmailsAsync(folder);
            }
            catch (Exception ex)
            {
                ShowDialog("Error", $"Failed to load {folder}: {ex.Message}", "Error");
            }
        }
    }

    private async void EmailListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmailListBox.SelectedItem is EmailItem email)
        {
            try
            {
                if (email.ThreadUserId > 0)
                {
                    await ShowThreadContentAsync(email.ThreadUserId, email.Sender);
                }
                else
                {
                    await ShowEmailContentAsync(email);
                }
            }
            catch (Exception ex)
            {
                ShowDialog("Error", $"Failed to open email: {ex.Message}", "Error");
            }
        }
    }

    private void WorkersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkersListBox.SelectedItem != null)
        {
            ShowComposeView(WorkersListBox.SelectedItem);
        }
    }

    private async Task ShowThreadContentAsync(int otherUserId, string otherUserName)
    {
        ContentArea.Children.Clear();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = $"Conversation · {otherUserName}",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(30, 30, 30, 10)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var list = new ListBox
        {
            Margin = new Thickness(30, 0, 30, 10),
            BorderThickness = new Thickness(0)
        };
        Grid.SetRow(list, 1);
        root.Children.Add(list);

        var composer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(30, 0, 30, 30)
        };

        var messageBox = new TextBox
        {
            MinHeight = 38,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 14,
            Margin = new Thickness(0, 0, 10, 0)
        };
        HintAssist.SetHint(messageBox, "Write a message...");

        var sendBtn = new Button
        {
            Content = "Send",
            Width = 100,
            Height = 38
        };

        composer.Children.Add(messageBox);
        composer.Children.Add(sendBtn);
        Grid.SetRow(composer, 2);
        root.Children.Add(composer);

        ContentArea.Children.Add(root);

        async Task ReloadAsync()
        {
            if (!_apiService.IsAuthenticated)
                return;

            var response = await _apiService.GetAsync<List<InternalThreadMessageDto>>($"/api/InternalEmail/threads/{otherUserId}");
            if (!response.Success || response.Data == null)
                return;

            list.ItemsSource = response.Data.Select(m =>
                $"[{(m.Direction == "in" ? otherUserName : "You")}] {m.SentAt.ToLocalTime():g}\n{m.Body}").ToList();
        }

        sendBtn.Click += async (_, _) =>
        {
            var text = (messageBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!_apiService.IsAuthenticated)
            {
                ShowDialog("Error", "Please authenticate first to send messages.", "Error");
                return;
            }

            var emailRequest = new
            {
                recipientIds = new[] { otherUserId },
                subject = "Message",
                body = text,
                attachments = Array.Empty<object>()
            };

            var sendResp = await _apiService.PostAsync<object, object>("/api/InternalEmail/send", emailRequest);
            if (sendResp.Success)
            {
                messageBox.Text = string.Empty;
                await ReloadAsync();
                await LoadEmailsAsync("Sent");
            }
            else
            {
                var errMsg = sendResp.Message ?? "Failed to send";
                if (sendResp.Errors != null && sendResp.Errors.Count > 0)
                    errMsg = string.Join("\n", sendResp.Errors);
                ShowDialog("Error", errMsg, "Error");
            }
        };

        await ReloadAsync();
    }

    private async Task ShowEmailContentAsync(EmailItem email)
    {
        // Mark as read server-side when opening an inbox item.
        if (_apiService.IsAuthenticated && email.Id != 0 && !email.IsRead && _currentFolder == "Inbox")
        {
            try
            {
                await _apiService.PutAsync<object, object>($"/api/InternalEmail/{email.Id}/read", new { });
                email.IsRead = true;
            }
            catch
            {
                // ignore
            }
        }

        // Fetch full email body if missing.
        if (_apiService.IsAuthenticated && email.Id != 0 && string.IsNullOrWhiteSpace(email.Body))
        {
            try
            {
                var full = await _apiService.GetAsync<InternalEmailDto>($"/api/InternalEmail/{email.Id}");
                if (full.Success && full.Data != null)
                {
                    email.Body = full.Data.Body;
                }
            }
            catch
            {
                // ignore
            }
        }

        ContentArea.Children.Clear();

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var contentPanel = new StackPanel
        {
            Margin = new Thickness(30)
        };

        // Header
        var headerPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 20)
        };

        var subjectText = new TextBlock
        {
            Text = email.Subject,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        };

        var senderGrid = new Grid();
        senderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        senderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        senderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var senderText = new TextBlock
        {
            Text = $"From: {email.Sender}",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(senderText, 0);

        var timeText = new TextBlock
        {
            Text = email.Time,
            FontSize = 12,
            Foreground = System.Windows.Media.Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 2);

        senderGrid.Children.Add(senderText);
        senderGrid.Children.Add(timeText);

        headerPanel.Children.Add(subjectText);
        headerPanel.Children.Add(senderGrid);

        var separator = new Separator
        {
            Margin = new Thickness(0, 15, 0, 20)
        };

        // Body
        var bodyText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(email.Body) ? email.Preview : email.Body,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22
        };

        contentPanel.Children.Add(headerPanel);
        contentPanel.Children.Add(separator);
        contentPanel.Children.Add(bodyText);

        scrollViewer.Content = contentPanel;
        ContentArea.Children.Add(scrollViewer);
    }

    private void ShowComposeView(object? selectedWorker = null)
    {
        ContentArea.Children.Clear();

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var composePanel = new StackPanel
        {
            Margin = new Thickness(30)
        };

        // Title
        var titleText = new TextBlock
        {
            Text = "New Message",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 20)
        };

        // To
        var toTextBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 15),
            FontSize = 14
        };
        HintAssist.SetHint(toTextBox, "To (email address or select from list)");

        if (selectedWorker != null)
        {
            var workerType = selectedWorker.GetType();
            var emailProp = workerType.GetProperty("Email");
            var nameProp = workerType.GetProperty("Name");
            if (emailProp != null && nameProp != null)
            {
                toTextBox.Text = $"{nameProp.GetValue(selectedWorker)} <{emailProp.GetValue(selectedWorker)}>";
            }
        }

        // Subject
        var subjectTextBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 15),
            FontSize = 14
        };
        HintAssist.SetHint(subjectTextBox, "Subject");

        // Body
        var bodyTextBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 300,
            Margin = new Thickness(0, 0, 0, 15),
            FontSize = 14
        };
        HintAssist.SetHint(bodyTextBox, "Message");

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var sendButton = new Button
        {
            Content = "Send",
            Width = 100,
            Height = 36,
            Margin = new Thickness(0, 0, 10, 0)
        };
        sendButton.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(toTextBox.Text) || 
                string.IsNullOrWhiteSpace(subjectTextBox.Text) || 
                string.IsNullOrWhiteSpace(bodyTextBox.Text))
            {
                ShowDialog("Validation Error", "Please fill in all fields (To, Subject, and Body)", "Warning");
                return;
            }

            try
            {
                var recipientInput = toTextBox.Text.Trim();
                
                // Extract email if format is "Name <email@example.com>"
                string recipientEmail = recipientInput;
                if (recipientInput.Contains('<') && recipientInput.Contains('>'))
                {
                    var start = recipientInput.IndexOf('<') + 1;
                    var end = recipientInput.IndexOf('>');
                    recipientEmail = recipientInput.Substring(start, end - start).Trim();
                }
                
                // Try to find recipient in registered users
                var recipient = _allWorkers.FirstOrDefault(w => 
                    w.Email.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase) ||
                    w.Name.Equals(recipientInput, StringComparison.OrdinalIgnoreCase));

                object emailRequest;
                bool isInternalRecipient = recipient != null;
                
                if (isInternalRecipient && recipient != null)
                {
                    // Send to registered user via internal email
                    emailRequest = new
                    {
                        recipientIds = new[] { recipient.Id },
                        subject = subjectTextBox.Text,
                        body = bodyTextBox.Text,
                        attachments = _pendingAttachments.Select(a => new
                        {
                            fileName = a.FileName,
                            filePath = a.FilePath,
                            fileSize = a.FileSize
                        }).ToArray()
                    };

                    if (!_apiService.IsAuthenticated)
                    {
                        ShowDialog("Error", "Please authenticate first to send emails.", "Error");
                        return;
                    }

                    var response = await _apiService.PostAsync<object, object>("/api/InternalEmail/send", emailRequest);
                    
                    if (response.Success)
                    {
                        ShowDialog("Success", $"Email sent successfully to {recipientInput}!", "Success");
                        _pendingAttachments.Clear();
                        ContentArea.Children.Clear();
                        ContentArea.Children.Add(new TextBlock
                        {
                            Text = "✓ Email sent successfully!",
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        await LoadEmailsAsync("Sent");
                    }
                    else
                    {
                        var errMsg = response.Message ?? "Failed to send email";
                        if (response.Errors != null && response.Errors.Count > 0)
                        {
                            errMsg = string.Join("\n", response.Errors);
                        }
                        ShowDialog("Error", errMsg, "Error");
                    }
                    return;
                }

                ShowDialog("Error", "Recipient must be an internal user. Please pick a contact from the workers list.", "Error");
            }
            catch (TaskCanceledException)
            {
                ShowDialog("Error", "Request timeout. Please check your network connection.", "Error");
            }
            catch (HttpRequestException ex)
            {
                ShowDialog("Error", $"Network error: {ex.Message}", "Error");
            }
            catch (Exception ex)
            {
                ShowDialog("Error", $"Failed to send email: {ex.Message}\n\nInner: {ex.InnerException?.Message}", "Error");
            }
        };

        var attachButton = new Button
        {
            Content = "Attach File",
            Width = 120,
            Height = 36,
            Margin = new Thickness(0, 0, 10, 0)
        };

        attachButton.Click += async (_, _) =>
        {
            if (!_apiService.IsAuthenticated)
            {
                ShowDialog("Error", "Please authenticate first to attach files.", "Error");
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Select attachment",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != true)
                return;

            var upload = await _apiService.UploadFileAsync<InternalEmailAttachmentDto>("/api/InternalEmail/attachments/upload", dlg.FileName);
            if (upload.Success && upload.Data != null)
            {
                _pendingAttachments.Add(upload.Data);
                ShowDialog("Success", $"Attached: {upload.Data.FileName}", "Success");
            }
            else
            {
                var msg = upload.Message ?? "Upload failed";
                if (upload.Errors != null && upload.Errors.Count > 0)
                    msg = string.Join("\n", upload.Errors);
                ShowDialog("Error", msg, "Error");
            }
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 100,
            Height = 36
        };
        cancelButton.Click += (s, e) =>
        {
            _pendingAttachments.Clear();
            ContentArea.Children.Clear();
            ContentArea.Children.Add(new TextBlock
            {
                Text = "Select an email to read",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        };

        buttonPanel.Children.Add(sendButton);
        buttonPanel.Children.Add(attachButton);
        buttonPanel.Children.Add(cancelButton);

        composePanel.Children.Add(titleText);
        composePanel.Children.Add(toTextBox);
        composePanel.Children.Add(subjectTextBox);
        composePanel.Children.Add(bodyTextBox);
        composePanel.Children.Add(buttonPanel);

        scrollViewer.Content = composePanel;
        ContentArea.Children.Add(scrollViewer);
    }

    private void ShowDialog(string title, string message, string type)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, 
            type == "Success" ? MessageBoxImage.Information : 
            type == "Error" ? MessageBoxImage.Error : 
            type == "Warning" ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private class WorkerItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
    }

    private class EmailItem
    {
        public int Id { get; set; }
        public int ThreadUserId { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string SenderInitials { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public bool HasAttachment { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }

    private class InternalThreadDto
    {
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public string OtherUserEmail { get; set; } = string.Empty;
        public string OtherUserInitials { get; set; } = string.Empty;
        public string OtherUserRole { get; set; } = string.Empty;
        public string OtherUserDepartment { get; set; } = string.Empty;
        public int LastEmailId { get; set; }
        public string LastSubject { get; set; } = string.Empty;
        public string LastPreview { get; set; } = string.Empty;
        public DateTime LastSentAt { get; set; }
        public string LastTime { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
    }

    private class InternalThreadMessageDto
    {
        public int EmailId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderInitials { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string Time { get; set; } = string.Empty;
        public bool IsRead { get; set; }
    }

    private class InternalEmailAttachmentDto
    {
        public int? Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    private class InternalEmailListResponseDto
    {
        public List<InternalEmailDto> Emails { get; set; } = new();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
    }

    private class InternalEmailDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderInitials { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string Time { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public List<object> Recipients { get; set; } = new();
        public List<object> Attachments { get; set; } = new();
        public bool HasAttachment { get; set; }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.Length <= max) return value;
        return value.Substring(0, max).TrimEnd() + "…";
    }

    private static string GetInitialsFallback(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
        return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
    }
}
