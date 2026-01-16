using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DigitalisationERP.Desktop.Models;

namespace DigitalisationERP.Desktop.Services.IoT
{
    /// <summary>
    /// Service de logging local vers fichiers CSV
    /// Enregistre tous les événements IoT pour audit et analyse
    /// </summary>
    public class LocalLogService
    {
        private readonly string _logDirectory;
        private readonly object _fileLock = new object();
        private string CurrentLogFile => Path.Combine(_logDirectory, $"activity_{DateTime.Now:yyyy-MM-dd}.csv");

        public LocalLogService(string logDirectory = "logs")
        {
            _logDirectory = logDirectory;
            
            // Créer le répertoire si nécessaire
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Initialiser le fichier CSV avec les en-têtes
            InitializeCsvFile();
        }

        /// <summary>
        /// Initialise le fichier CSV avec les en-têtes si nécessaire
        /// </summary>
        private void InitializeCsvFile()
        {
            lock (_fileLock)
            {
                if (!File.Exists(CurrentLogFile))
                {
                    var header = "Timestamp,Level,Source,EventType,Message,Value,Unit,Location\n";
                    File.WriteAllText(CurrentLogFile, header, Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// Enregistre un événement de log générique
        /// </summary>
        public async Task LogEventAsync(string level, string source, string eventType, string message, string? location = null)
        {
            await LogEventInternalAsync(DateTime.Now, level, source, eventType, message, null, null, location);
        }

        /// <summary>
        /// Enregistre une lecture de capteur
        /// </summary>
        public async Task LogSensorReadingAsync(IotSensorReading reading)
        {
            string eventType = reading.State switch
            {
                SensorState.CRITICAL => "SENSOR_CRITICAL",
                SensorState.WARNING => "SENSOR_WARNING",
                _ => "SENSOR_READ"
            };

            string level = reading.State switch
            {
                SensorState.CRITICAL => "ERROR",
                SensorState.WARNING => "WARN",
                _ => "INFO"
            };

            await LogEventInternalAsync(
                reading.Timestamp,
                level,
                reading.SensorId,
                eventType,
                $"{reading.Type} reading",
                reading.Value,
                reading.Unit,
                reading.PostCode
            );
        }

        /// <summary>
        /// Enregistre un changement d'état de robot
        /// </summary>
        public async Task LogRobotStateAsync(RobotState robot)
        {
            await LogEventInternalAsync(
                robot.LastUpdate,
                "INFO",
                robot.RobotId,
                "ROBOT_STATE",
                $"{robot.Status} - {robot.CurrentTask}",
                robot.BatteryLevel,
                "%",
                robot.CurrentLocation
            );
        }

        /// <summary>
        /// Enregistre une commande envoyée à un robot
        /// </summary>
        public async Task LogRobotCommandAsync(string robotId, string command, string? targetLocation = null)
        {
            await LogEventInternalAsync(
                DateTime.Now,
                "INFO",
                robotId,
                "ROBOT_COMMAND",
                $"Command: {command}",
                null,
                null,
                targetLocation
            );
        }

        /// <summary>
        /// Enregistre une alerte critique
        /// </summary>
        public async Task LogCriticalAlertAsync(string sensorId, string message, double value, double threshold, string location)
        {
            await LogEventInternalAsync(
                DateTime.Now,
                "CRITICAL",
                sensorId,
                "ALERT",
                $"{message} (Threshold: {threshold})",
                value,
                null,
                location
            );
        }

        /// <summary>
        /// Méthode interne d'écriture CSV
        /// </summary>
        private Task LogEventInternalAsync(
            DateTime timestamp,
            string level,
            string source,
            string eventType,
            string message,
            double? value,
            string? unit,
            string? location)
        {
            try
            {
                var csvLine = $"{timestamp:yyyy-MM-dd HH:mm:ss}," +
                             $"{EscapeCsv(level)}," +
                             $"{EscapeCsv(source)}," +
                             $"{EscapeCsv(eventType)}," +
                             $"{EscapeCsv(message)}," +
                             $"{(value.HasValue ? value.Value.ToString("F2") : "")}," +
                             $"{EscapeCsv(unit ?? "")}," +
                             $"{EscapeCsv(location ?? "")}\n";

                lock (_fileLock)
                {
                    File.AppendAllText(CurrentLogFile, csvLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Ne pas planter l'application si le logging échoue
                System.Diagnostics.Debug.WriteLine($"[LocalLogService] Erreur d'écriture: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Échappe les caractères spéciaux pour le format CSV
        /// </summary>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        /// <summary>
        /// Exporte les logs d'une période vers un fichier
        /// </summary>
        public async Task<string> ExportLogsAsync(DateTime startDate, DateTime endDate, string outputPath)
        {
            var allLogs = new List<string>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var logFile = Path.Combine(_logDirectory, $"activity_{currentDate:yyyy-MM-dd}.csv");
                if (File.Exists(logFile))
                {
                    var lines = await File.ReadAllLinesAsync(logFile);
                    
                    // Ajouter toutes les lignes sauf l'en-tête (sauf pour le premier fichier)
                    if (allLogs.Count == 0)
                    {
                        allLogs.AddRange(lines);
                    }
                    else
                    {
                        allLogs.AddRange(lines.Skip(1));
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            if (allLogs.Count > 0)
            {
                await File.WriteAllLinesAsync(outputPath, allLogs, Encoding.UTF8);
                return outputPath;
            }

            throw new InvalidOperationException("Aucun log trouvé pour la période spécifiée");
        }

        /// <summary>
        /// Archive les logs plus anciens que X jours
        /// </summary>
        public async Task ArchiveOldLogsAsync(int daysToKeep = 30)
        {
            var cutoffDate = DateTime.Now.Date.AddDays(-daysToKeep);
            var archiveDirectory = Path.Combine(_logDirectory, "archive");

            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            var logFiles = Directory.GetFiles(_logDirectory, "activity_*.csv");

            foreach (var logFile in logFiles)
            {
                var fileName = Path.GetFileName(logFile);
                
                // Extraire la date du nom de fichier (format: activity_YYYY-MM-DD.csv)
                if (fileName.StartsWith("activity_") && fileName.EndsWith(".csv"))
                {
                    var datePart = fileName.Substring(9, 10); // YYYY-MM-DD
                    if (DateTime.TryParse(datePart, out var fileDate))
                    {
                        if (fileDate < cutoffDate)
                        {
                            var archivePath = Path.Combine(archiveDirectory, fileName);
                            File.Move(logFile, archivePath, true);
                            System.Diagnostics.Debug.WriteLine($"[LocalLogService] Archivé: {fileName}");
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Obtient les statistiques des logs pour une journée
        /// </summary>
        public async Task<LogStatistics> GetDailyStatisticsAsync(DateTime date)
        {
            var logFile = Path.Combine(_logDirectory, $"activity_{date:yyyy-MM-dd}.csv");
            var stats = new LogStatistics { Date = date };

            if (!File.Exists(logFile))
            {
                return stats;
            }

            var lines = await File.ReadAllLinesAsync(logFile);
            
            // Ignorer l'en-tête
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    var level = parts[1].Trim('"');
                    var eventType = parts[3].Trim('"');

                    stats.TotalEvents++;

                    switch (level.ToUpperInvariant())
                    {
                        case "ERROR":
                        case "CRITICAL":
                            stats.CriticalEvents++;
                            break;
                        case "WARN":
                        case "WARNING":
                            stats.WarningEvents++;
                            break;
                    }

                    if (eventType.Contains("SENSOR"))
                        stats.SensorReadings++;
                    else if (eventType.Contains("ROBOT"))
                        stats.RobotEvents++;
                }
            }

            return stats;
        }
    }

    /// <summary>
    /// Statistiques d'une journée de logs
    /// </summary>
    public class LogStatistics
    {
        public DateTime Date { get; set; }
        public int TotalEvents { get; set; }
        public int CriticalEvents { get; set; }
        public int WarningEvents { get; set; }
        public int SensorReadings { get; set; }
        public int RobotEvents { get; set; }
    }
}
