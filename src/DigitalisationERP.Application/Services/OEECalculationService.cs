using System;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service de calcul OEE (Overall Equipment Effectiveness) en temps réel
    /// OEE = Disponibilité × Performance × Qualité
    /// </summary>
    public class OEECalculationService : IOEECalculationService
    {
        /// <summary>
        /// Calcule OEE en fonction des métriques de production
        /// </summary>
        public OEEMetrics CalculateOEE(
            int plannedMinutes,
            int actualRunTime,
            int idleTime,
            int producedUnits,
            int expectedUnits,
            int defectiveUnits)
        {
            // DISPONIBILITÉ = (Temps planifié - Temps d'arrêt) / Temps planifié × 100
            var availability = plannedMinutes > 0
                ? ((double)(plannedMinutes - idleTime) / plannedMinutes) * 100
                : 0;

            // PERFORMANCE = Unités produites / Unités attendues × 100
            var performance = expectedUnits > 0
                ? ((double)producedUnits / expectedUnits) * 100
                : 0;

            // QUALITÉ = (Unités produites - Défectueuses) / Unités produites × 100
            var quality = producedUnits > 0
                ? ((double)(producedUnits - defectiveUnits) / producedUnits) * 100
                : 0;

            // Plafonner à 100%
            availability = Math.Min(availability, 100);
            performance = Math.Min(performance, 100);
            quality = Math.Min(quality, 100);

            // OEE GLOBAL = Dispo × Perf × Qualité
            var oee = (availability / 100) * (performance / 100) * (quality / 100) * 100;

            return new OEEMetrics
            {
                Availability = Math.Round(availability, 2),
                Performance = Math.Round(performance, 2),
                Quality = Math.Round(quality, 2),
                OEE = Math.Round(oee, 2),
                PlannedProductionTime = plannedMinutes * 60,  // Convertir en secondes
                ActualProductionTime = actualRunTime * 60,
                IdleTime = idleTime * 60,
                ProducedUnits = producedUnits,
                DefectiveUnits = defectiveUnits,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Détermine la couleur de santé basée sur OEE vs seuil minimum
        /// </summary>
        public string GetOEEHealthColor(double oeeValue, double minOEE)
        {
            var minOEEPercent = minOEE * 100;

            if (oeeValue >= minOEEPercent)
                return "🟢 Good";

            if (oeeValue >= minOEEPercent * 0.8)
                return "🟡 Warning";

            return "🔴 Critical";
        }
    }
}
