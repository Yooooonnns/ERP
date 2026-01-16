using System;
using System.Collections.Generic;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Plan de production avec calcul automatique du Takt Time
    /// </summary>
    public class ProductionPlan
    {
        public int PlanId { get; set; }
        public int LineId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime TargetDeadline { get; set; }

        // Quantités
        public int QuantityToProduce { get; set; } = 0;
        public int QuantityProduced { get; set; } = 0;

        // Timing
        public double AvailableHours { get; set; } = 0;
        public double CalculatedTaktTime { get; set; } = 0;  // En secondes

        // État
        public string Status { get; set; } = "Planning";  // "Planning" "Active" "Completed" "Aborted"

        // Relations
        public virtual ProductionLine Line { get; set; } = null!;
        public virtual List<OEEMetrics> Metrics { get; set; } = new();

        /// <summary>
        /// Calcule automatiquement le Takt Time
        /// Takt Time = Temps disponible / Quantité requise
        /// </summary>
        public void CalculateTaktTime()
        {
            if (QuantityToProduce <= 0)
            {
                CalculatedTaktTime = 0;
                return;
            }

            // Convertir heures en secondes
            double availableSeconds = AvailableHours * 3600;
            CalculatedTaktTime = availableSeconds / QuantityToProduce;
        }

        // Propriétés calculées
        public double RequiredCadence => CalculatedTaktTime > 0 ? 3600 / CalculatedTaktTime : 0;
        public int ProductionProgress => QuantityToProduce > 0 ? (int)((QuantityProduced / (double)QuantityToProduce) * 100) : 0;
        public TimeSpan TimeRemaining => TargetDeadline - DateTime.UtcNow;
        public bool IsDeadlineExceeded => DateTime.UtcNow > TargetDeadline;
    }
}
