# Digitalisation ERP - Desktop Application

## Overview
WPF Desktop application for the Digitalisation ERP system. Provides a Windows native interface for user authentication, email verification, and administrative functions.

## Features

### 🔐 Authentication
- **Login**: Secure login with JWT tokens
- **Registration**: Self-service user registration with email verification
- **Email Verification**: Token-based email verification system
- **Password Reset**: Forgot password functionality (coming soon)

### 👥 User Management (S-User/Admin)
- **Create User Accounts**: Admins can create new user accounts
- **Automatic Credential Delivery**: Optionally send login credentials via email
- **Role Assignment**: Assign multiple roles to users:
  - S-User (Admin)
  - Production
  - Maintenance
  - Warehouse
  - Quality
  - Planning

### 🎨 User Interface
- **Material Design**: Modern, professional UI using MaterialDesignInXaml
- **Responsive Layout**: Clean, intuitive navigation
- **Real-time Feedback**: Loading indicators and success/error messages
- **Email Status Indicators**: Visual feedback for email verification status

## Prerequisites

- .NET 9.0 SDK or later
- Windows OS
- Backend API running (default: http://localhost:5000)

## Getting Started

### 1. Start the Backend API

First, ensure the backend API is running:

```powershell
# From the solution root
docker-compose up -d

# Or run the API directly
cd src/DigitalisationERP.API
dotnet run
```

The API should be running on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### 2. Configure API URL (Optional)

The Desktop and Launcher read the API base URL from:

- Environment variable: `DIGITALISATIONERP_API_BASE_URL` (highest priority)
- Or the runtime settings file: `digitalisationerp.settings.json` (copied next to the .exe)

Example (PowerShell):

```powershell
$env:DIGITALISATIONERP_API_BASE_URL = "http://your-api-url:port"
```

### 3. Run the Desktop Application

```powershell
cd src/DigitalisationERP.Desktop
dotnet run
```

Or build and run the executable:

```powershell
dotnet build
cd bin/Debug/net9.0-windows
.\DigitalisationERP.Desktop.exe
```

## Usage

### First Time Setup

1. **Start with Admin Account**:
   - Default admin credentials: `admin` / `Admin123!`
   - Admin has `SAP_ALL` role with full access

2. **Login as Admin**:
   - Launch the application
   - Enter admin credentials
   - Click "SIGN IN"

3. **Admin Dashboard Opens**:
   - Create new user accounts
   - Assign roles
   - Send credentials automatically

### Regular User Workflow

1. **Register** (if self-registration is enabled):
   - Click "Create Account" on login screen
   - Fill in registration form
   - Submit and check email for verification code

2. **Verify Email**:
   - Click "Verify Email" on login screen
   - Enter verification token from email
   - Or use resend functionality if needed

3. **Login**:
   - Enter username and password
   - Application validates email verification status
   - Dashboard opens on successful login

### Admin Workflow (S-User)

1. **Login as Admin**
2. **Create User Account**:
   - Fill in user details (username, email, password)
   - Select user roles (at least one required)
   - Check "Send credentials via email" to auto-notify user
   - Click "CREATE USER ACCOUNT"
   - System automatically:
     - Creates the account
     - Sends welcome email with credentials
     - Marks email as verified (no verification needed for admin-created accounts)

## Architecture

### Technology Stack
- **Framework**: WPF (.NET 9.0)
- **UI Library**: MaterialDesignInXaml 5.3.0
- **HTTP Client**: System.Net.Http.Json 10.0.0
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection 10.0.0
- **Pattern**: MVVM (Model-View-ViewModel)

### Project Structure

```
DigitalisationERP.Desktop/
├── Models/               # Data transfer objects
│   ├── LoginRequest.cs
│   ├── LoginResponse.cs
│   ├── RegisterRequest.cs
│   ├── CreateUserRequest.cs
│   └── ApiResponse.cs
├── ViewModels/           # MVVM ViewModels
│   ├── LoginViewModel.cs
│   ├── RegisterViewModel.cs
│   ├── VerifyEmailViewModel.cs
│   └── CreateUserViewModel.cs
├── Views/                # XAML Views
│   ├── LoginWindow.xaml
│   ├── RegisterWindow.xaml
│   ├── VerifyEmailWindow.xaml
│   ├── AdminDashboardWindow.xaml
│   └── MainWindow.xaml
├── Services/             # Business logic
│   ├── ApiService.cs     # HTTP client wrapper
│   └── AuthService.cs    # Authentication logic
├── Helpers/              # Utilities
│   ├── ViewModelBase.cs
│   ├── RelayCommand.cs
│   └── Converters.cs
└── App.xaml             # Application entry point
```

### Communication Flow

```
Desktop UI → AuthService → ApiService → HTTP → Backend API → Database
    ↓                                              ↓
Email Verification Token                    Email Service → SMTP
```

## API Endpoints Used

The desktop application communicates with these backend endpoints:

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration (with email verification)
- `POST /api/auth/verify-email` - Verify email token
- `POST /api/auth/resend-verification` - Resend verification email
- `POST /api/auth/forgot-password` - Password reset request
- `POST /api/auth/reset-password` - Reset password with token
- `POST /api/auth/create-user` - Admin creates user (requires S_USER role)

## Configuration

### Email Configuration
Email settings are configured in the backend `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-email@company.com",
    "SenderName": "ERP System",
    "Username": "your-email@company.com",
    "Password": "your-app-password",
    "EnableSsl": true
  }
}
```

### Database
Backend uses PostgreSQL with these tables:
- `users` - User accounts
- `roles` - User roles
- `email_verification_tokens` - Verification tokens
- `email_queue` - Outgoing email queue

## Features In Detail

### Login Window
- Username/password authentication
- Email verification status check
- Links to:
  - Registration
  - Email verification
  - Password reset
- Material Design UI

### Registration Window
- Required fields:
  - Username
  - Email
  - Password
  - Confirm Password
- Optional fields:
  - Full Name
  - Phone Number
  - Department
- Automatic email verification token sent
- Real-time validation

### Email Verification Window
- Enter verification token
- Resend verification email option
- Token expiry: 24 hours
- One-time use tokens

### Admin Dashboard (S-User)
- Create user accounts
- Assign multiple roles
- Automatic credential email
- Password generation (or custom)
- Form validation
- Success/error feedback

### Main Dashboard (Regular Users)
- Welcome screen
- Future modules:
  - Materials Management
  - Production Planning
  - Maintenance Management
  - IoT Monitoring
  - Reports

## Security

### Authentication
- JWT token-based authentication
- 8-hour access token expiry
- Secure password storage (BCrypt on backend)
- Role-based access control (RBAC)

### Email Verification
- 24-hour token expiry
- One-time use tokens
- Cryptographically secure token generation
- Automatic token cleanup

### Transport Security
- HTTPS recommended for production
- Token transmitted in Authorization header
- Password never stored in plaintext

## Troubleshooting

### Application Won't Start
- Ensure .NET 9.0 SDK is installed
- Check Windows compatibility
- Verify no port conflicts

### Can't Connect to API
- Verify backend API is running: `curl http://localhost:5000/health`
- If using a custom URL, check `DIGITALISATIONERP_API_BASE_URL` or `digitalisationerp.settings.json`
- Ensure firewall allows connections
- Check Docker containers: `docker ps`

### Email Not Received
- Check backend email configuration
- Verify SMTP settings in `appsettings.json`
- Check spam folder
- Test SMTP credentials manually
- Review backend logs: `docker logs digitalisation-erp-api`

### Login Fails
- Verify email is verified (for self-registered users)
- Check username/password
- Admin account doesn't require verification
- Check API logs for errors

### Email Verification Token Invalid
- Token expires after 24 hours
- Use "Resend Code" to get new token
- Each token can only be used once
- Check email for most recent token

## Development

### Building
```powershell
dotnet build
```

### Running Tests (future)
```powershell
dotnet test
```

### Publishing
```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

Creates standalone executable in `bin/Release/net9.0-windows/win-x64/publish/`

## Future Enhancements

- [ ] Password reset flow UI
- [ ] Remember me functionality
- [ ] Auto-update system
- [ ] Offline mode
- [ ] Multi-language support
- [ ] Dark theme option
- [ ] User profile management
- [ ] Change password
- [ ] Materials management module
- [ ] Production planning module
- [ ] Maintenance management module
- [ ] IoT dashboard
- [ ] Reports generation

## Support

For issues or questions:
1. Check backend API logs
2. Verify email configuration
3. Test API endpoints with Postman
4. Review this documentation

## Related Documentation

- [Email Implementation Guide](../../docs/EMAIL_GUIDE.md)
- [Authentication Flows](../../docs/AUTHENTICATION_FLOWS.md)
- [Email Quick Start](../../docs/EMAIL_QUICKSTART.md)
- [Backend API Documentation](../DigitalisationERP.API/README.md)

## License

Internal use only - Digitalisation ERP System
