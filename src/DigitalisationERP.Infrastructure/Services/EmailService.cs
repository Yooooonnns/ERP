using DigitalisationERP.Application.DTOs.Email;
using DigitalisationERP.Application.Interfaces;
using DigitalisationERP.Core.Entities.Auth;
using DigitalisationERP.Core.Entities.System;
using DigitalisationERP.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Text;
using System.Text.Json;
using Resend;
using EmailStatus = DigitalisationERP.Core.Entities.System.EmailStatus;
using EmailAttachment = DigitalisationERP.Application.DTOs.Email.EmailAttachment;

namespace DigitalisationERP.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    // SMTP Configuration
    private string SmtpHost => _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
    private int SmtpPort => int.Parse(_configuration["Email:SmtpPort"] ?? "587");
    private string SmtpUsername => _configuration["Email:SmtpUsername"] ?? "";
    private string SmtpPassword => _configuration["Email:SmtpPassword"] ?? "";
    private string FromEmail => _configuration["Email:FromEmail"] ?? "";
    private string FromName => _configuration["Email:FromName"] ?? "DigitalisationERP";
    private bool EnableSsl => bool.Parse(_configuration["Email:EnableSsl"] ?? "true");

    public EmailService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<long> SendEmailAsync(SendEmailRequest request)
    {
        var emailQueue = new EmailQueue
        {
            ToEmail = request.ToEmail,
            ToName = request.ToName,
            CcEmails = request.CcEmails != null ? string.Join(",", request.CcEmails) : null,
            BccEmails = request.BccEmails != null ? string.Join(",", request.BccEmails) : null,
            Subject = request.Subject,
            Body = request.Body,
            Priority = request.Priority,
            Status = EmailStatus.Pending,
            ScheduledAt = request.ScheduledAt,
            HasAttachments = request.Attachments?.Any() ?? false,
            ClientId = "001"
        };

        if (request.Attachments?.Any() ?? false)
        {
            emailQueue.AttachmentPaths = JsonSerializer.Serialize(request.Attachments);
        }

        _context.EmailQueue.Add(emailQueue);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email queued: {EmailId} to {ToEmail}", emailQueue.Id, request.ToEmail);

        // If no schedule, send immediately
        if (!request.ScheduledAt.HasValue)
        {
            await SendQueuedEmailAsync(emailQueue.Id);
        }

        return emailQueue.Id;
    }

    public async Task<long> SendTemplateEmailAsync(SendTemplateEmailRequest request)
    {
        var body = GetEmailTemplate(request.TemplateName, request.TemplateData);

        var emailRequest = new SendEmailRequest
        {
            ToEmail = request.ToEmail,
            ToName = request.ToName,
            Subject = request.TemplateData.ContainsKey("Subject") 
                ? request.TemplateData["Subject"].ToString() ?? ""
                : "Notification",
            Body = body,
            CcEmails = request.CcEmails,
            Priority = request.Priority,
            ScheduledAt = request.ScheduledAt
        };

        return await SendEmailAsync(emailRequest);
    }

    public async Task<long> SendVerificationEmailAsync(SendVerificationEmailRequest request)
    {
        var token = await GenerateEmailVerificationTokenAsync(request.UserId, request.Email);
        var verificationUrl = $"{request.CallbackUrl}?token={token}";

        var templateData = new Dictionary<string, object>
        {
            { "Subject", "Verify Your Email Address" },
            { "VerificationUrl", verificationUrl },
            { "VerificationCode", token },
            { "Email", request.Email }
        };

        // Log verification code to console for development
        _logger.LogWarning("==========================================================");
        _logger.LogWarning("VERIFICATION CODE FOR {Email}: {Token}", request.Email, token);
        _logger.LogWarning("==========================================================");

        var emailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
        {
            ToEmail = request.Email,
            TemplateName = "EmailVerification",
            TemplateData = templateData,
            Priority = 1 // High priority
        });

        _logger.LogInformation("Email verification sent to {Email}", request.Email);
        return emailId;
    }

    public async Task<long> SendPasswordResetEmailAsync(SendPasswordResetEmailRequest request)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", "Reset Your Password" },
            { "ResetUrl", request.ResetUrl },
            { "Email", request.Email }
        };

        var emailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
        {
            ToEmail = request.Email,
            TemplateName = "PasswordReset",
            TemplateData = templateData,
            Priority = 1 // High priority
        });

        _logger.LogInformation("Password reset email sent to {Email}", request.Email);
        return emailId;
    }

    public async Task<long> SendPasswordChangedNotificationAsync(string email, string username)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", "Password Changed Successfully" },
            { "Username", username },
            { "ChangeDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
        };

        var emailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
        {
            ToEmail = email,
            TemplateName = "PasswordChanged",
            TemplateData = templateData,
            Priority = 1
        });

        _logger.LogInformation("Password changed notification sent to {Email}", email);
        return emailId;
    }

    public async Task<long> SendWelcomeEmailAsync(string email, string username, string fullName)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", "Welcome to DigitalisationERP" },
            { "Username", username },
            { "FullName", fullName }
        };

        var emailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
        {
            ToEmail = email,
            TemplateName = "Welcome",
            TemplateData = templateData
        });

        _logger.LogInformation("Welcome email sent to {Email}", email);
        return emailId;
    }

    public async Task<long> SendProductionReportAsync(SendProductionReportRequest request)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", $"{request.ReportType} Production Report - {request.ReportDate:yyyy-MM-dd}" },
            { "ReportDate", request.ReportDate.ToString("MMMM dd, yyyy") },
            { "ReportType", request.ReportType }
        };

        long lastEmailId = 0;
        foreach (var recipient in request.Recipients)
        {
            var emailRequest = new SendEmailRequest
            {
                ToEmail = recipient,
                Subject = templateData["Subject"].ToString()!,
                Body = GetEmailTemplate("ProductionReport", templateData),
                Priority = 2
            };

            if (request.PdfReport != null)
            {
                emailRequest.Attachments = new List<EmailAttachment>
                {
                    new EmailAttachment
                    {
                        FileName = $"ProductionReport_{request.ReportDate:yyyyMMdd}.pdf",
                        Content = request.PdfReport,
                        ContentType = "application/pdf"
                    }
                };
            }

            lastEmailId = await SendEmailAsync(emailRequest);
        }

        _logger.LogInformation("Production report sent to {Count} recipients", request.Recipients.Count);
        return lastEmailId;
    }

    public async Task<long> SendStockAlertAsync(SendStockAlertRequest request)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", $"Stock Alert: {request.MaterialName} ({request.AlertType})" },
            { "MaterialNumber", request.MaterialNumber },
            { "MaterialName", request.MaterialName },
            { "CurrentStock", request.CurrentStock },
            { "MinStockLevel", request.MinStockLevel },
            { "AlertType", request.AlertType }
        };

        long lastEmailId = 0;
        foreach (var recipient in request.Recipients)
        {
            lastEmailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
            {
                ToEmail = recipient,
                TemplateName = "StockAlert",
                TemplateData = templateData,
                Priority = request.AlertType == "Critical" ? 1 : 2
            });
        }

        _logger.LogInformation("Stock alert sent for material {MaterialNumber}", request.MaterialNumber);
        return lastEmailId;
    }

    public async Task<long> SendMaintenanceAlertAsync(SendMaintenanceAlertRequest request)
    {
        var templateData = new Dictionary<string, object>
        {
            { "Subject", $"Maintenance Alert: {request.MachineName} - {request.AlertType}" },
            { "MachineNumber", request.MachineNumber },
            { "MachineName", request.MachineName },
            { "AlertType", request.AlertType },
            { "Description", request.Description ?? "N/A" },
            { "ScheduledDate", request.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "Immediate" }
        };

        long lastEmailId = 0;
        foreach (var recipient in request.Recipients)
        {
            lastEmailId = await SendTemplateEmailAsync(new SendTemplateEmailRequest
            {
                ToEmail = recipient,
                TemplateName = "MaintenanceAlert",
                TemplateData = templateData,
                Priority = request.AlertType == "Critical" || request.AlertType == "Breakdown" ? 1 : 2
            });
        }

        _logger.LogInformation("Maintenance alert sent for machine {MachineNumber}", request.MachineNumber);
        return lastEmailId;
    }

    public async Task<List<EmailQueueDto>> GetPendingEmailsAsync()
    {
        var emails = await _context.EmailQueue
            .Where(e => e.Status == EmailStatus.Pending || e.Status == EmailStatus.Failed)
            .Where(e => !e.ScheduledAt.HasValue || e.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.CreatedOn)
            .Take(100)
            .ToListAsync();

        return emails.Select(e => new EmailQueueDto
        {
            Id = e.Id,
            ToEmail = e.ToEmail,
            Subject = e.Subject,
            Status = e.Status.ToString(),
            ScheduledAt = e.ScheduledAt,
            SentAt = e.SentAt,
            SendAttempts = e.SendAttempts,
            ErrorMessage = e.ErrorMessage,
            CreatedOn = e.CreatedOn
        }).ToList();
    }

    public async Task<EmailQueueDto?> GetEmailStatusAsync(long emailId)
    {
        var email = await _context.EmailQueue.FindAsync(emailId);
        if (email == null) return null;

        return new EmailQueueDto
        {
            Id = email.Id,
            ToEmail = email.ToEmail,
            Subject = email.Subject,
            Status = email.Status.ToString(),
            ScheduledAt = email.ScheduledAt,
            SentAt = email.SentAt,
            SendAttempts = email.SendAttempts,
            ErrorMessage = email.ErrorMessage,
            CreatedOn = email.CreatedOn
        };
    }

    public async Task<bool> ProcessEmailQueueAsync()
    {
        var pendingEmails = await _context.EmailQueue
            .Where(e => e.Status == EmailStatus.Pending || 
                       (e.Status == EmailStatus.Failed && e.SendAttempts < e.MaxAttempts))
            .Where(e => !e.ScheduledAt.HasValue || e.ScheduledAt <= DateTime.UtcNow)
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.CreatedOn)
            .Take(10)
            .ToListAsync();

        foreach (var email in pendingEmails)
        {
            await SendQueuedEmailAsync(email.Id);
        }

        return true;
    }

    public async Task<bool> CancelEmailAsync(long emailId)
    {
        var email = await _context.EmailQueue.FindAsync(emailId);
        if (email == null || email.Status == EmailStatus.Sent)
            return false;

        email.Status = EmailStatus.Cancelled;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email {EmailId} cancelled", emailId);
        return true;
    }

    public async Task<bool> RetryEmailAsync(long emailId)
    {
        var email = await _context.EmailQueue.FindAsync(emailId);
        if (email == null)
            return false;

        email.Status = EmailStatus.Pending;
        email.ErrorMessage = null;
        await _context.SaveChangesAsync();

        await SendQueuedEmailAsync(emailId);
        return true;
    }

    private async Task<bool> SendQueuedEmailAsync(long emailId)
    {
        var emailQueue = await _context.EmailQueue.FindAsync(emailId);
        if (emailQueue == null)
            return false;

        try
        {
            emailQueue.Status = EmailStatus.Sending;
            emailQueue.SendAttempts++;
            await _context.SaveChangesAsync();

            // Use Resend SDK
            var resendApiKey = _configuration["Resend:ApiKey"];
            if (string.IsNullOrEmpty(resendApiKey))
            {
                throw new InvalidOperationException("Resend API key is not configured. Please set Resend:ApiKey in appsettings.json");
            }

            var fromEmail = _configuration["Resend:FromEmail"] ?? "noreply@norepley.digitalerp.com";
            var fromName = _configuration["Resend:FromName"] ?? "DigitalisationERP";

            _logger.LogWarning("==========================================================");
            _logger.LogWarning("📧 SENDING EMAIL VIA RESEND");
            _logger.LogWarning("To: {ToEmail}", emailQueue.ToEmail);
            _logger.LogWarning("From: {FromEmail}", fromEmail);
            _logger.LogWarning("Subject: {Subject}", emailQueue.Subject);
            _logger.LogWarning("==========================================================");

            // Create Resend client
            var resend = ResendClient.Create(resendApiKey);

            // Send email using Resend SDK
            var message = new Resend.EmailMessage
            {
                From = fromEmail,
                To = new[] { emailQueue.ToEmail },
                Subject = emailQueue.Subject,
                HtmlBody = emailQueue.Body
            };

            // Add CC recipients
            if (!string.IsNullOrEmpty(emailQueue.CcEmails))
            {
                message.Cc = emailQueue.CcEmails.Split(',').Select(e => e.Trim()).ToArray();
            }

            // Add BCC recipients
            if (!string.IsNullOrEmpty(emailQueue.BccEmails))
            {
                message.Bcc = emailQueue.BccEmails.Split(',').Select(e => e.Trim()).ToArray();
            }

            _logger.LogInformation("Calling Resend API...");
            var response = await resend.EmailSendAsync(message);

            _logger.LogInformation("Resend Response: {@Response}", response);

            // Check if response is successful
            if (response != null)
            {
                emailQueue.Status = EmailStatus.Sent;
                emailQueue.SentAt = DateTime.UtcNow;
                emailQueue.ErrorMessage = null;
                await _context.SaveChangesAsync();

                _logger.LogWarning("==========================================================");
                _logger.LogWarning("✅ EMAIL SENT SUCCESSFULLY to {ToEmail} via Resend!", emailQueue.ToEmail);
                _logger.LogWarning("==========================================================");
                return true;
            }
            else
            {
                throw new Exception("Resend API error: No response");
            }
        }
        catch (Exception ex)
        {
            emailQueue.Status = emailQueue.SendAttempts >= emailQueue.MaxAttempts 
                ? EmailStatus.Failed 
                : EmailStatus.Pending;
            emailQueue.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Failed to send email {EmailId} (Attempt {Attempt}/{Max})", 
                emailId, emailQueue.SendAttempts, emailQueue.MaxAttempts);
            return false;
        }
    }

    public async Task<string> GenerateEmailVerificationTokenAsync(long userId, string email)
    {
        var token = Guid.NewGuid().ToString("N");

        var verificationToken = new EmailVerificationToken
        {
            UserId = userId,
            Token = token,
            Email = email,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false,
            TokenType = "EmailVerification",
            ClientId = "001"
        };

        _context.EmailVerificationTokens.Add(verificationToken);
        await _context.SaveChangesAsync();

        return token;
    }

    public async Task<bool> VerifyEmailTokenAsync(string token)
    {
        var verificationToken = await _context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed);

        if (verificationToken == null)
            return false;

        if (verificationToken.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Email verification token expired: {Token}", token);
            return false;
        }

        verificationToken.IsUsed = true;
        verificationToken.UsedAt = DateTime.UtcNow;

        // Mark user email as verified (you may want to add EmailVerified field to User entity)
        // verificationToken.User.EmailVerified = true;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified for user {UserId}", verificationToken.UserId);
        return true;
    }

    public string GetEmailTemplate(string templateName, Dictionary<string, object> data)
    {
        return templateName switch
        {
            "EmailVerification" => GetEmailVerificationTemplate(data),
            "PasswordReset" => GetPasswordResetTemplate(data),
            "PasswordChanged" => GetPasswordChangedTemplate(data),
            "Welcome" => GetWelcomeTemplate(data),
            "ProductionReport" => GetProductionReportTemplate(data),
            "StockAlert" => GetStockAlertTemplate(data),
            "MaintenanceAlert" => GetMaintenanceAlertTemplate(data),
            _ => GetDefaultTemplate(data)
        };
    }

    private string GetEmailVerificationTemplate(Dictionary<string, object> data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .code-box {{ background: #fff; border: 2px solid #2196F3; border-radius: 8px; padding: 20px; margin: 20px 0; text-align: center; }}
        .code {{ font-family: 'Courier New', monospace; font-size: 24px; font-weight: bold; color: #2196F3; letter-spacing: 2px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🏭 DigitalisationERP</h1>
        </div>
        <div class='content'>
            <h2>Verify Your Email Address</h2>
            <p>Hello,</p>
            <p>Thank you for registering with DigitalisationERP. Please verify your email address using the verification code below:</p>
            <div class='code-box'>
                <p style='margin: 0 0 10px 0; color: #666; font-size: 14px;'>Your Verification Code:</p>
                <div class='code'>{data["VerificationCode"]}</div>
            </div>
            <p>Or click the button below to verify automatically:</p>
            <div style='text-align: center;'>
                <a href='{data["VerificationUrl"]}' class='button'>Verify Email</a>
            </div>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; color: #666;'>{data["VerificationUrl"]}</p>
            <p>This verification code will expire in 24 hours.</p>
            <p>If you didn't create an account, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetPasswordResetTemplate(Dictionary<string, object> data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #FF5722; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 30px; background: #FF5722; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Password Reset</h1>
        </div>
        <div class='content'>
            <h2>Reset Your Password</h2>
            <p>Hello,</p>
            <p>We received a request to reset your password. Click the button below to create a new password:</p>
            <div style='text-align: center;'>
                <a href='{data["ResetUrl"]}' class='button'>Reset Password</a>
            </div>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; color: #666;'>{data["ResetUrl"]}</p>
            <div class='warning'>
                <strong>⚠️ Security Notice:</strong> This link will expire in 1 hour. If you didn't request a password reset, please ignore this email and ensure your account is secure.
            </div>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetPasswordChangedTemplate(Dictionary<string, object> data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .success {{ background: #d4edda; border-left: 4px solid #28a745; padding: 10px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Password Changed</h1>
        </div>
        <div class='content'>
            <h2>Your Password Has Been Changed</h2>
            <p>Hello {data["Username"]},</p>
            <div class='success'>
                Your password was successfully changed on {data["ChangeDate"]}.
            </div>
            <p>If you made this change, you can safely ignore this email.</p>
            <p>If you did NOT make this change, please contact your system administrator immediately.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetWelcomeTemplate(Dictionary<string, object> data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .feature {{ padding: 10px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Welcome to DigitalisationERP!</h1>
        </div>
        <div class='content'>
            <h2>Hello {data["FullName"]}!</h2>
            <p>Welcome to our manufacturing ERP system. Your account has been successfully created.</p>
            <p><strong>Username:</strong> {data["Username"]}</p>
            <h3>Getting Started:</h3>
            <div class='feature'>📦 Manage materials and inventory</div>
            <div class='feature'>🏭 Plan and track production orders</div>
            <div class='feature'>🔧 Schedule preventive maintenance</div>
            <div class='feature'>🤖 Control robotic automation</div>
            <div class='feature'>📊 Monitor IoT sensors in real-time</div>
            <p>If you have any questions, please contact your system administrator.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetProductionReportTemplate(Dictionary<string, object> data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #3F51B5; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📊 Production Report</h1>
        </div>
        <div class='content'>
            <h2>{data["ReportType"]} Report - {data["ReportDate"]}</h2>
            <p>Your {data["ReportType"].ToString()!.ToLower()} production report is ready.</p>
            <p>The detailed PDF report is attached to this email.</p>
            <p><strong>Report Date:</strong> {data["ReportDate"]}</p>
            <p>Please review the report and contact your supervisor if you have any questions.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetStockAlertTemplate(Dictionary<string, object> data)
    {
        var alertColor = data["AlertType"].ToString() == "Critical" ? "#dc3545" : "#ffc107";
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: {alertColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .alert-box {{ background: #fff3cd; border-left: 4px solid {alertColor}; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ Stock Alert</h1>
        </div>
        <div class='content'>
            <h2>{data["AlertType"]} Stock Level</h2>
            <div class='alert-box'>
                <p><strong>Material:</strong> {data["MaterialNumber"]} - {data["MaterialName"]}</p>
                <p><strong>Current Stock:</strong> {data["CurrentStock"]}</p>
                <p><strong>Minimum Level:</strong> {data["MinStockLevel"]}</p>
            </div>
            <p>Action Required: Please review inventory and initiate procurement if necessary.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetMaintenanceAlertTemplate(Dictionary<string, object> data)
    {
        var alertColor = data["AlertType"].ToString() == "Critical" || data["AlertType"].ToString() == "Breakdown" ? "#dc3545" : "#17a2b8";
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: {alertColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .alert-box {{ background: #f8d7da; border-left: 4px solid {alertColor}; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔧 Maintenance Alert</h1>
        </div>
        <div class='content'>
            <h2>{data["AlertType"]} Maintenance</h2>
            <div class='alert-box'>
                <p><strong>Machine:</strong> {data["MachineNumber"]} - {data["MachineName"]}</p>
                <p><strong>Alert Type:</strong> {data["AlertType"]}</p>
                <p><strong>Scheduled:</strong> {data["ScheduledDate"]}</p>
                <p><strong>Description:</strong> {data["Description"]}</p>
            </div>
            <p>Please assign a technician and schedule the maintenance work immediately.</p>
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    public async Task SendApprovalEmailAsync(SendApprovalEmailRequest request)
    {
        var emailRequest = new SendTemplateEmailRequest
        {
            ToEmail = request.Email,
            ToName = request.FirstName,
            TemplateName = "AccountApproval",
            TemplateData = new Dictionary<string, object>
            {
                { "FirstName", request.FirstName },
                { "Message", request.Message ?? "Your account has been successfully validated. You can now log in to the application." }
            }
        };

        try
        {
            await SendTemplateEmailAsync(emailRequest);
            _logger.LogInformation($"Approval email sent to {request.Email}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending approval email to {request.Email}: {ex.Message}");
            throw;
        }
    }

    public async Task SendRejectionEmailAsync(SendRejectionEmailRequest request)
    {
        var emailRequest = new SendTemplateEmailRequest
        {
            ToEmail = request.Email,
            ToName = request.FirstName,
            TemplateName = "AccountRejection",
            TemplateData = new Dictionary<string, object>
            {
                { "FirstName", request.FirstName },
                { "Reason", request.Reason ?? "Your account application did not meet our requirements. Please contact support for more information." }
            }
        };

        try
        {
            await SendTemplateEmailAsync(emailRequest);
            _logger.LogInformation($"Rejection email sent to {request.Email}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending rejection email to {request.Email}: {ex.Message}");
            throw;
        }
    }

    private string GetDefaultTemplate(Dictionary<string, object> data)
    {
        var body = new StringBuilder();
        foreach (var kvp in data)
        {
            if (kvp.Key != "Subject")
            {
                body.AppendLine($"<p><strong>{kvp.Key}:</strong> {kvp.Value}</p>");
            }
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #607D8B; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background: #f9f9f9; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📧 Notification</h1>
        </div>
        <div class='content'>
            {body}
        </div>
        <div class='footer'>
            <p>© 2025 DigitalisationERP. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
}
