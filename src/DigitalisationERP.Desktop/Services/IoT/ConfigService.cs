using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DigitalisationERP.Desktop.Services.IoT
{
    /// <summary>
    /// Service de gestion de la configuration IoT
    /// Charge et sauvegarde iot_config.json
    /// </summary>
    public class ConfigService
    {
        private readonly string _configFilePath;
        private IotConfiguration? _currentConfig;

        public ConfigService(string configFilePath = "iot_config.json")
        {
            // IMPORTANT:
            // In published apps, the working directory can be different (e.g., system32 or a shortcut folder).
            // Always resolve relative config paths next to the executable so COM settings + OF payloads load.
            _configFilePath = Path.IsPathRooted(configFilePath)
                ? configFilePath
                : Path.Combine(AppContext.BaseDirectory, configFilePath);
        }

        /// <summary>
        /// Obtient la configuration actuelle (charge si nécessaire)
        /// </summary>
        public async Task<IotConfiguration> GetConfigurationAsync()
        {
            if (_currentConfig == null)
            {
                await LoadConfigurationAsync();
            }

            return _currentConfig ?? new IotConfiguration();
        }

        /// <summary>
        /// Charge la configuration depuis le fichier JSON                                                                                              
        /// </summary>
        public async Task<IotConfiguration> LoadConfigurationAsync()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // Créer une configuration par défaut si le fichier n'existe pas
                    _currentConfig = new IotConfiguration();
                    await SaveConfigurationAsync(_currentConfig);
                    return _currentConfig;
                }

                string json = await File.ReadAllTextAsync(_configFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                _currentConfig = JsonSerializer.Deserialize<IotConfiguration>(json, options);
                
                if (_currentConfig == null)
                {
                    throw new InvalidOperationException("La désérialisation a retourné null");
                }

                return _currentConfig;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erreur lors du chargement de la configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sauvegarde la configuration dans le fichier JSON
        /// </summary>
        public async Task SaveConfigurationAsync(IotConfiguration config)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(_configFilePath, json);
                _currentConfig = config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erreur lors de la sauvegarde de la configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Met à jour les seuils d'un capteur
        /// </summary>
        public async Task UpdateSensorThresholdsAsync(string sensorId, double warning, double critical)
        {
            var config = await GetConfigurationAsync();
            var sensor = config.Sensors.Find(s => s.Id == sensorId);

            if (sensor != null)
            {
                sensor.Thresholds.Warning = warning;
                sensor.Thresholds.Critical = critical;
                await SaveConfigurationAsync(config);
            }
            else
            {
                throw new ArgumentException($"Capteur {sensorId} introuvable dans la configuration");
            }
        }

        /// <summary>
        /// Obtient la configuration d'un capteur spécifique
        /// </summary>
        public async Task<SensorConfiguration?> GetSensorConfigAsync(string sensorId)
        {
            var config = await GetConfigurationAsync();
            return config.Sensors.Find(s => s.Id == sensorId);
        }

        /// <summary>
        /// Obtient la configuration d'un robot spécifique
        /// </summary>
        public async Task<RobotConfiguration?> GetRobotConfigAsync(string robotId)
        {
            var config = await GetConfigurationAsync();
            return config.Robots.Find(r => r.Id == robotId);
        }

        /// <summary>
        /// Détermine quel provider IoT utiliser selon la configuration
        /// </summary>
        public async Task<string> GetProviderTypeAsync()
        {
            var config = await GetConfigurationAsync();
            return config.Provider.ToLowerInvariant();
        }
    }
}
