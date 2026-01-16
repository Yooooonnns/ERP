param(
  [string]$Source = "$PSScriptRoot\artifacts\DesktopPublish",
  [string]$Destination = "$env:USERPROFILE\Desktop\DigitalisationERP_Publish",
  [switch]$SkipPublish = $false
)

$ErrorActionPreference = 'Stop'

Write-Host "Stopping running ERP processes (best-effort)..."
foreach ($name in @('DigitalisationERP.Desktop','DigitalisationERP.Launcher','DigitalisationERP.API')) {
  try { Stop-Process -Name $name -Force -ErrorAction Stop } catch { }
}

if (-not $SkipPublish) {
  Write-Host "Publishing projects to artifacts..."

  if (Test-Path $Source) {
    Remove-Item -Recurse -Force $Source
  }
  New-Item -ItemType Directory -Force -Path $Source | Out-Null

  $launcherCsproj = Join-Path $PSScriptRoot 'src\DigitalisationERP.Launcher\DigitalisationERP.Launcher.csproj'
  $desktopCsproj  = Join-Path $PSScriptRoot 'src\DigitalisationERP.Desktop\DigitalisationERP.Desktop.csproj'
  $apiCsproj      = Join-Path $PSScriptRoot 'src\DigitalisationERP.API\DigitalisationERP.API.csproj'

  $desktopTemp = Join-Path $Source '_desktop_temp'
  $apiOut = Join-Path $Source 'API'
  $desktopOut = Join-Path $Source 'Desktop'

  dotnet publish $launcherCsproj -c Release -r win-x64 --self-contained false -o $Source
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish Launcher failed with exit code $LASTEXITCODE" }

  dotnet publish $desktopCsproj -c Release -r win-x64 --self-contained false -o $desktopTemp
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish Desktop failed with exit code $LASTEXITCODE" }

  dotnet publish $apiCsproj -c Release -r win-x64 --self-contained false -o $apiOut
  if ($LASTEXITCODE -ne 0) { throw "dotnet publish API failed with exit code $LASTEXITCODE" }

  # Place Desktop binaries in root (for Launcher compatibility) and also under Desktop/.
  # IMPORTANT: do NOT use /MIR into root, otherwise it will delete API/ and Launcher files.
  robocopy $desktopTemp $Source /E /R:2 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy desktop->root failed with exit code $LASTEXITCODE" }

  New-Item -ItemType Directory -Force -Path $desktopOut | Out-Null
  robocopy $desktopTemp $desktopOut /MIR /R:2 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy desktop->Desktop/ failed with exit code $LASTEXITCODE" }

  Remove-Item -Recurse -Force $desktopTemp
}

Write-Host "Copying publish artifacts..."
if (!(Test-Path $Source)) {
  throw "Source folder not found: $Source"
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null

# Robocopy exit codes: 0-7 are success, >=8 are failures.
robocopy $Source $Destination /MIR /R:2 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) {
  throw "robocopy failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Published to: $Destination"