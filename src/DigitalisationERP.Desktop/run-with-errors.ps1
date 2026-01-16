try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "$PSScriptRoot\bin\Debug\net9.0-windows\DigitalisationERP.Desktop.exe"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $process.Start() | Out-Null
    
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    
    Write-Host "Exit Code: $($process.ExitCode)"
    if ($stdout) { Write-Host "STDOUT: $stdout" }
    if ($stderr) { Write-Host "STDERR: $stderr" }
    
    # Also check Windows Event Log for .NET errors
    Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 1 -ErrorAction SilentlyContinue | 
        Where-Object { $_.TimeGenerated -gt (Get-Date).AddMinutes(-1) } |
        Format-List TimeGenerated, Message
}
catch {
    Write-Host "Error: $_"
}
