using System;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Poste de production dans une ligne
    /// </summary>
    public class ProductionPost
    {
    public int PostId { get; set; }
    public int LineId { get; set; }
    public int ProductionLineId { get; set; }          // Alias for LineId
    public string PostCode { get; set; } = "";        // "POST-01"
    public string PostName { get; set; } = "";        // "Cutting Station"
    public int Position { get; set; }                  // Ordre (0, 1, 2...)        // Paramètres
        public string PostType { get; set; } = "";        // "Semi-Auto" "Manual" "Robot"
        public int TargetCadence { get; set; } = 0;       // Pièces/heure
        public int MaxCapacity { get; set; } = 0;         // Capacité max

        // État temps réel
        public int CurrentStock { get; set; } = 0;        // En attente
        public int ProcessedUnits { get; set; } = 0;      // Traitées
        public string Status { get; set; } = "Offline";   // "Online" "Offline" "Maintenance"
        public DateTime LastStatusChange { get; set; } = DateTime.UtcNow;

        // Maintenance
        public double MaintenanceHealthScore { get; set; } = 100;
        public string MaintenanceIssue { get; set; } = "";

        // Relations
        public virtual ProductionLine Line { get; set; } = null!;

        // Propriétés calculées
        public bool IsHealthy => MaintenanceHealthScore >= 80;
        public bool IsCritical => MaintenanceHealthScore < 50;
        public int StockPercentage => MaxCapacity > 0 ? (int)((CurrentStock / (double)MaxCapacity) * 100) : 0;
    }
}
