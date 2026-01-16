using System;
using System.Diagnostics;
using System.IO;

namespace DigitalisationERP.SimpleApp;

class Program
{
    static void Main()
    {
        try
        {
            // Tuer les anciens processus
            foreach (var proc in Process.GetProcessesByName("dotnet"))
            {
                try { proc.Kill(); } catch { }
            }

            // Chercher le Launcher
            string launcherPath = @"C:\Users\Yooonns\Desktop\Projects\DigitalisationERP\src\DigitalisationERP.Launcher\bin\Release\net9.0-windows\DigitalisationERP.Launcher.exe";
            
            if (!File.Exists(launcherPath))
            {
                // Fallback - chercher depuis le répertoire de l'exe
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                launcherPath = Path.Combine(exeDir, "../../../Launcher/bin/Release/net9.0-windows/DigitalisationERP.Launcher.exe");
                launcherPath = Path.GetFullPath(launcherPath);
            }

            if (File.Exists(launcherPath))
            {
                var launcher = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = launcherPath,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };

                launcher.Start();
            }
        }
        catch (Exception ex)
        {
            // Silent fail - l'app se lance juste pas
        }
    }
}
