using System;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Lecture de données SCADA temps réel d'un poste
    /// </summary>
    public class SCADAReading
    {
        public int ReadingId { get; set; }
        public int PostId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // État poste
        public string Status { get; set; } = "";           // "Running" "Stopped" "Error"
        public int CurrentStock { get; set; } = 0;
        public int UnitsProducedLastHour { get; set; } = 0;

        // Métriques
        public double Temperature { get; set; } = 0;
        public double Vibration { get; set; } = 0;
        public int ErrorCount { get; set; } = 0;

        // Relations
        public virtual ProductionPost Post { get; set; } = null!;
    }
}
