# DigitalisationERP - Complete Installation Guide

A comprehensive Enterprise Resource Planning system with a .NET 9 API backend and WPF desktop application.

## 📋 Table of Contents

- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
- [Detailed Setup Instructions](#detailed-setup-instructions)
- [Project Structure](#project-structure)
- [Running the Application](#running-the-application)
- [Database Setup](#database-setup)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## 🖥️ System Requirements

### Required Software
- **Windows 10/11** (for desktop app)
- **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **PostgreSQL 14+** ([download](https://www.postgresql.org/download/windows/))
- **Redis** ([download](https://github.com/microsoftarchive/redis/releases) or use WSL)
- **Git** ([download](https://git-scm.com/download/win))

### Optional Tools
- **Visual Studio 2022** (Community Edition is free)
- **pgAdmin** (for database management)
- **Postman** (for API testing)

### Hardware
- Minimum: 4GB RAM, 2GB disk space
- Recommended: 8GB RAM, 5GB disk space

---

## 🚀 Quick Start

For the impatient (assumes all prerequisites installed):

```powershell
# Clone the repository
git clone https://github.com/Yooooonnns/ERP.git
cd ERP

# Build the solution
dotnet build

# Setup database (see Database Setup section)
# ... configure databases first ...

# Run the API (Terminal 1)
cd src/DigitalisationERP.API
dotnet run

# Run the Desktop App (Terminal 2)
cd src/DigitalisationERP.Launcher
dotnet run
```

---

## 📥 Detailed Setup Instructions

### Step 1: Install .NET 9 SDK

```powershell
# Verify installation
dotnet --version
# Should show: 9.0.x
```

### Step 2: Clone the Repository

```powershell
# Choose a location (avoid spaces in path if possible)
cd C:\Projects
git clone https://github.com/Yooooonnns/ERP.git
cd ERP
```

### Step 3: Install & Configure PostgreSQL

**On Windows:**
1. Download PostgreSQL installer from https://www.postgresql.org/download/windows/
2. Run the installer
3. During installation:
   - Set password for `postgres` user (remember this!)
   - Port: 5432 (default)
   - Locale: Your locale

**After Installation:**
```powershell
# Test PostgreSQL connection
psql -U postgres -h localhost
# You should see the psql prompt (PostgreSQL #)
# Exit with: \q
```

### Step 4: Create Databases & Users

Open **pgAdmin** (comes with PostgreSQL) or use command line:

```powershell
# Open PostgreSQL command line
psql -U postgres -h localhost

# Create users
CREATE USER erpuser WITH PASSWORD 'ErpPassword123!';
CREATE USER iotuser WITH PASSWORD 'IotPassword123!';

# Create databases
CREATE DATABASE digitalisation_erp OWNER erpuser;
CREATE DATABASE sensor_data OWNER iotuser;

# Grant privileges
GRANT ALL PRIVILEGES ON DATABASE digitalisation_erp TO erpuser;
GRANT ALL PRIVILEGES ON DATABASE sensor_data TO iotuser;

# Exit
\q
```

### Step 5: Install & Configure Redis

**Option A: Windows (Official)**
1. Download from: https://github.com/microsoftarchive/redis/releases
2. Extract and run `redis-server.exe`
3. Default port: 6379

**Option B: Windows Subsystem for Linux (WSL)**
```bash
# In WSL terminal
sudo apt-get install redis-server
redis-server
```

### Step 6: Build the Solution

```powershell
cd C:\Projects\ERP
dotnet build

# Expected output: Build succeeded with 0 errors
```

---

## 📂 Project Structure

```
ERP/
├── src/
│   ├── DigitalisationERP.API/              # ASP.NET Core API
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── appsettings.json                # ⚠️ Database config here
│   │   └── Program.cs
│   │
│   ├── DigitalisationERP.Desktop/          # WPF Desktop UI
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   └── App.xaml
│   │
│   ├── DigitalisationERP.Launcher/         # WPF Application Launcher
│   │   ├── Program.cs
│   │   └── SplashWindow.xaml
│   │
│   ├── DigitalisationERP.Application/      # Business Logic
│   ├── DigitalisationERP.Domain/           # Domain Models
│   ├── DigitalisationERP.Infrastructure/   # Data Access
│   ├── DigitalisationERP.Core/             # Shared Utilities
│   │
│   └── DigitalisationERP.Domain.Identity/
│       └── Infrastructure.Identity/        # Authentication/Authorization
│
├── DigitalisationERP.sln                   # Main solution file
├── README.md                               # This file
└── .gitignore                              # Git exclusions
```

---

## ▶️ Running the Application

### Terminal 1: Start the API Server

```powershell
cd src/DigitalisationERP.API
dotnet run

# Expected output:
# Building...
# info: Microsoft.Hosting.Lifetime[14]
# Now listening on: http://localhost:5000
# Now listening on: https://localhost:5001
```

### Optional: Seed Accounts (recommended after a fresh reset)

The API seeds base roles/authorizations automatically. If you reset your PC (new database), you can recreate an **admin** account and/or an **S_USER** account using environment variables (so no passwords are stored in Git).

```powershell
# Admin account (optional)
$env:ERP_SEED_ADMIN = "true"
$env:ERP_ADMIN_USERNAME = "admin"
$env:ERP_ADMIN_EMAIL = "admin@erp.local"
$env:ERP_ADMIN_PASSWORD = "CHANGE_THIS_PASSWORD"

# Standard user account (optional)
$env:ERP_SEED_SUSER_EMAIL = "user@example.com"
$env:ERP_SEED_SUSER_PASSWORD = "CHANGE_THIS_PASSWORD"

cd src/DigitalisationERP.API
dotnet run
```

After first startup, the users will be created in PostgreSQL (if they don't already exist).

**Keep this terminal open!** The API must be running for the desktop app to work.

The Desktop + Launcher read the API base URL from (in order):

- Environment variable: `DIGITALISATIONERP_API_BASE_URL`
- Or the runtime file: `digitalisationerp.settings.json` (copied next to the .exe)

### Terminal 2: Start the Desktop Application

```powershell
cd src/DigitalisationERP.Launcher
dotnet run

# A splash screen will appear, then the login window
```

### Testing the API

```powershell
# In a 3rd terminal, test with curl
curl http://localhost:5000/health

# If using a custom API URL:
# $env:DIGITALISATIONERP_API_BASE_URL = "http://your-api-url:port"

# Or use Postman:
# - GET http://localhost:5000/api/health
```

---

## 🗄️ Database Setup

### Automatic Migration

The application automatically applies database migrations on first run. However, you can manually apply them:

```powershell
cd src/DigitalisationERP.API

# Apply migrations
dotnet ef database update --project ../DigitalisationERP.Infrastructure

# Verify with pgAdmin or psql
```

### Verify Database Connection

After starting the API, check the logs for connection messages:

```
info: DigitalisationERP.Infrastructure[0]
Database connection successful
```

If you see connection errors, verify:
1. PostgreSQL is running (`psql -U postgres -h localhost`)
2. Users and databases exist (see Database Setup section)
3. Credentials in `appsettings.json` are correct

---

## ⚙️ Configuration

### API Configuration

**File:** `src/DigitalisationERP.API/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=digitalisation_erp;Username=erpuser;Password=ErpPassword123!",
    "TimescaleConnection": "Host=localhost;Port=5433;Database=sensor_data;Username=iotuser;Password=IotPassword123!",
    "RedisConnection": "localhost:6379"
  },
  "JWT": {
    "Secret": "your-secret-key-here-at-least-32-characters-long!",
    "Issuer": "DigitalisationERP",
    "Audience": "DigitalisationERPUsers",
    "ExpirationMinutes": 60
  }
}
```

### Changing Database Credentials

If you used different passwords during setup:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=digitalisation_erp;Username=erpuser;Password=YOUR_PASSWORD_HERE!",
```

### Changing API Port

**File:** `src/DigitalisationERP.API/Properties/launchSettings.json`

```json
"applicationUrl": "https://localhost:5001;http://localhost:5000"
```

---

## 🔧 Troubleshooting

### Issue: "Cannot connect to database"

**Diagnosis:**
```powershell
# Check if PostgreSQL is running
psql -U postgres -h localhost
# If fails, PostgreSQL is not running

# Check if the database exists
psql -U erpuser -h localhost -d digitalisation_erp
# If fails, database doesn't exist
```

**Solution:**
1. Start PostgreSQL service
2. Create databases (see Database Setup)
3. Verify credentials in `appsettings.json`

---

### Issue: "Port 5000 already in use"

```powershell
# Find what's using port 5000
netstat -ano | findstr :5000

# Kill the process (replace PID)
taskkill /PID <PID> /F

# Or change API port in launchSettings.json
```

---

### Issue: "Redis connection failed"

**Diagnosis:**
```powershell
# Check if Redis is running
redis-cli ping
# Should respond: PONG
```

**Solution:**
1. Start Redis server
2. If not available, install from https://github.com/microsoftarchive/redis/releases

---

### Issue: "Migrations not applied"

```powershell
cd src/DigitalisationERP.API

# Force database update
dotnet ef database update --force --project ../DigitalisationERP.Infrastructure

# Check migration status
dotnet ef migrations list --project ../DigitalisationERP.Infrastructure
```

---

### Issue: "Build fails with missing dependencies"

```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Try building again
dotnet build
```

---

### Issue: "Desktop app won't start"

**Check logs:**
```powershell
# Logs are saved to Desktop
cat ~/Desktop/DigitalisationERP_Launcher.log

# Common issues:
# 1. API not running (start Terminal 1 first)
# 2. API address wrong in app config
# 3. Network issues (firewall blocking localhost)
```

---

## 🔐 Default Credentials

After first run, use these to log in:

| Field | Value |
|-------|-------|
| Email | admin@digitalisationerp.com |
| Password | Admin@123 |
| Role | Administrator |

⚠️ **Change these credentials immediately in production!**

---

## 📚 Additional Resources

- [.NET 9 Documentation](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-9)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [WPF Documentation](https://learn.microsoft.com/dotnet/desktop/wpf/)

---

## 🤝 Support & Issues

If you encounter issues not covered here:

1. Check the logs:
   - API: Console output in Terminal 1
   - Desktop: `~/Desktop/DigitalisationERP_Launcher.log`

2. Verify all prerequisites are installed:
   ```powershell
   dotnet --version
   psql --version
   redis-cli --version
   git --version
   ```

3. Check GitHub issues: [ERP Issues](https://github.com/Yooooonnns/ERP/issues)

---

## 📝 License

This project is proprietary software. All rights reserved.

---

## ✨ Happy coding!

**Last Updated:** December 24, 2025
**Project Version:** 1.0.0
