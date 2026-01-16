# Script de lancement ERP
param(
    [switch]$SkipAPI = $false
)

Write-Host "Lancement de DigitalisationERP..." -ForegroundColor Green

# Definir les chemins
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$desktopExe = Join-Path $projectRoot "src\DigitalisationERP.Desktop\bin\Release\net9.0-windows\DigitalisationERP.Desktop.exe"
$apiProject = Join-Path $projectRoot "src\DigitalisationERP.API\DigitalisationERP.API.csproj"

function Get-ApiBaseUrl {
    $envUrl = [Environment]::GetEnvironmentVariable("DIGITALISATIONERP_API_BASE_URL")
    if (-not [string]::IsNullOrWhiteSpace($envUrl)) {
        return $envUrl.Trim().TrimEnd('/')
    }

    $settingsPath = Join-Path $projectRoot "digitalisationerp.settings.json"
    if (Test-Path $settingsPath) {
        try {
            $json = Get-Content -Path $settingsPath -Raw | ConvertFrom-Json
            if ($null -ne $json -and -not [string]::IsNullOrWhiteSpace($json.ApiBaseUrl)) {
                return ($json.ApiBaseUrl.ToString()).Trim().TrimEnd('/')
            }
        } catch {
            # ignore malformed settings
        }
    }

    return "http://localhost:5000"
}

$apiBaseUrl = Get-ApiBaseUrl
$apiHealthUrl = "$apiBaseUrl/health"

function Test-ApiHealthy {
    try {
        $resp = Invoke-WebRequest -Uri $apiHealthUrl -UseBasicParsing -TimeoutSec 2
        return $resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300
    } catch {
        return $false
    }
}

function Wait-ApiReady {
    param(
        [int]$Seconds = 20
    )
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-ApiHealthy) { return $true }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

# Verifier si l'executable desktop existe
if (-not (Test-Path $desktopExe)) {
    Write-Host "Executable desktop non trouve. Construction en cours..." -ForegroundColor Yellow
    dotnet build "src/DigitalisationERP.Desktop" --configuration Release
    
    if (-not (Test-Path $desktopExe)) {
        Write-Host "Erreur: Impossible de construire l'application desktop" -ForegroundColor Red
        Read-Host "Appuyez sur Entree pour continuer..."
        exit 1
    }
}

try {
    # Demarrer l'API (sauf si SkipAPI)
    if (-not $SkipAPI) {
        if (Test-ApiHealthy) {
            Write-Host "API déjà démarrée (health OK)." -ForegroundColor Green
        } else {
            Write-Host "Démarrage de l'API..." -ForegroundColor Cyan

            $apiPublishExe = Join-Path $projectRoot "src\DigitalisationERP.API\bin\Release\net9.0\win-x64\publish\DigitalisationERP.API.exe"
            $apiDebugDll  = Join-Path $projectRoot "src\DigitalisationERP.API\bin\Debug\net9.0\DigitalisationERP.API.dll"

            if (Test-Path $apiPublishExe) {
                Start-Process -FilePath $apiPublishExe -WorkingDirectory (Split-Path -Parent $apiPublishExe) | Out-Null
            } elseif (Test-Path $apiDebugDll) {
                Start-Process -FilePath "dotnet" -ArgumentList "\"$apiDebugDll\"" -WorkingDirectory (Split-Path -Parent $apiDebugDll) -WindowStyle Hidden | Out-Null
            } else {
                Write-Host "Artifacts API non trouvés, build en cours..." -ForegroundColor Yellow
                dotnet build "$apiProject" -c Release
                if (Test-Path $apiPublishExe) {
                    Start-Process -FilePath $apiPublishExe -WorkingDirectory (Split-Path -Parent $apiPublishExe) | Out-Null
                } else {
                    # Fallback: run from csproj
                    Start-Process -FilePath "dotnet" -ArgumentList "run --project \"$apiProject\" --urls $apiBaseUrl" -WorkingDirectory $projectRoot -WindowStyle Hidden | Out-Null
                }
            }

            if (-not (Wait-ApiReady -Seconds 30)) {
                Write-Host "ATTENTION: L'API n'a pas répondu sur /health ($apiHealthUrl)." -ForegroundColor Yellow
            } else {
                Write-Host "API prête (health OK)." -ForegroundColor Green
            }
        }
    }

    # Demarrer l'application desktop
    Write-Host "Lancement de l'interface desktop..." -ForegroundColor Cyan
    Set-Location $projectRoot
    
    # Lancer l'application avec affichage des erreurs
    $process = Start-Process -FilePath $desktopExe -WorkingDirectory $projectRoot -PassThru
    Write-Host "Processus lance avec ID: $($process.Id)" -ForegroundColor Green
    
    # Attendre un peu pour voir si le processus demarre correctement
    Start-Sleep -Seconds 3
    
    if ($process.HasExited) {
        Write-Host "ATTENTION: Le processus s'est ferme immediatement (Code: $($process.ExitCode))" -ForegroundColor Red
        Write-Host "Cela peut indiquer un probleme de dependances ou de configuration." -ForegroundColor Yellow
        Read-Host "Appuyez sur Entree pour continuer..."
    } else {
        Write-Host "Application lancee avec succes!" -ForegroundColor Green
    }
    
} catch {
    Write-Host "Erreur lors du lancement: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Details: $($_.Exception.StackTrace)" -ForegroundColor Gray
    Read-Host "Appuyez sur Entree pour continuer..."
    exit 1
}