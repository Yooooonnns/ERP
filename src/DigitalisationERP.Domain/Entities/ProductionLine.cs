using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Ligne de production avec configuration des seuils OEE
    /// </summary>
    public class ProductionLine
    {
        public int LineId { get; set; }
        public int UserId { get; set; }
        public string LineName { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Paramètres de configuration
        public double TaktTime { get; set; } = 0;
        public int TargetCadence { get; set; } = 0;

        // Seuils OEE
        public double MinAvailability { get; set; } = 85;      // %
        public double MinPerformance { get; set; } = 90;       // %
        public double MinQuality { get; set; } = 98;           // %

        // Relations
        public virtual User User { get; set; } = null!;
        public virtual List<ProductionPost> Posts { get; set; } = new();
        public virtual List<ProductionPlan> Plans { get; set; } = new();
        public virtual List<OEEMetrics> Metrics { get; set; } = new();

        // Propriétés calculées
        public double MinOEE => (MinAvailability / 100) * (MinPerformance / 100) * (MinQuality / 100);
        public int TotalPosts => Posts.Count;
        public double AverageHealthScore => Posts.Any() ? Posts.Average(p => p.MaintenanceHealthScore) : 0;
        public int CriticalPosts => Posts.Count(p => p.MaintenanceHealthScore < 50);

        public double CalculateMinOEEPercentage() => MinOEE * 100;
    }
}
