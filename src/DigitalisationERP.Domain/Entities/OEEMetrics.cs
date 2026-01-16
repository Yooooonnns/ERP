using System;
using DigitalisationERP.Domain;

namespace DigitalisationERP.Domain.Entities;
    /// <summary>
    /// Métriques OEE (Overall Equipment Effectiveness)
    /// OEE = Disponibilité × Performance × Qualité
    /// </summary>
    public class OEEMetrics
    {
        public int MetricId { get; set; }
        public int LineId { get; set; }
        public int? PlanId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Composants OEE (en pourcentage)
        public double Availability { get; set; }            // % (0-100)
        public double Performance { get; set; }             // % (0-100)
        public double Quality { get; set; }                 // % (0-100)

        // OEE Global
        public double OEE { get; set; }                     // % (0-100)
        public double OEEPercent { get; set; }              // % (0-100) - alias for OEE
        public int CalculatedTime { get; set; }             // Time in seconds

        // Détails
        public int PlannedProductionTime { get; set; }      // En secondes
        public int ActualProductionTime { get; set; }       // En secondes
        public int IdleTime { get; set; }                   // En secondes
        public int ProducedUnits { get; set; }              // Pièces produites
        public int DefectiveUnits { get; set; }             // Pièces défectueuses

        // Relations
        public virtual ProductionLine Line { get; set; } = null!;
        public virtual ProductionPlan? Plan { get; set; }

        // Propriétés calculées
        public string HealthStatus
        {
            get
            {
                if (OEE >= 85) return "🟢 Good";
                if (OEE >= 70) return "🟡 Warning";
                return "🔴 Critical";
            }
        }

        public string AvailabilityStatus
        {
            get
            {
                if (Availability >= 95) return "🟢 Excellent";
                if (Availability >= 85) return "🟡 Good";
                return "🔴 Poor";
            }
        }

        public string PerformanceStatus
        {
            get
            {
                if (Performance >= 95) return "🟢 Excellent";
                if (Performance >= 85) return "🟡 Good";
                return "🔴 Poor";
            }
        }

        public string QualityStatus
        {
            get
            {
                if (Quality >= 98) return "🟢 Excellent";
                if (Quality >= 95) return "🟡 Good";
                return "🔴 Poor";
            }
        }
    }
