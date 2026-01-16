using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Service de planification production avec calcul TaktTime automatique
    /// </summary>
    public class ProductionPlanningService : IProductionPlanningService
    {
        private static readonly List<ProductionPlan> _plans = new();
        private static int _nextPlanId = 1;

        public async Task<Result<ProductionPlan>> CreatePlanAsync(
            int lineId,
            int quantityToProduce,
            DateTime targetDeadline)
        {
            await Task.Delay(100);

            if (quantityToProduce <= 0)
                return Result<ProductionPlan>.Fail("Quantity must be greater than 0");

            if (targetDeadline <= DateTime.UtcNow)
                return Result<ProductionPlan>.Fail("Deadline must be in the future");

            // Calculer heures disponibles
            var availableHours = (targetDeadline - DateTime.UtcNow).TotalHours;

            // Créer le plan
            var plan = new ProductionPlan
            {
                PlanId = _nextPlanId++,
                LineId = lineId,
                QuantityToProduce = quantityToProduce,
                AvailableHours = availableHours,
                TargetDeadline = targetDeadline,
                CreatedDate = DateTime.UtcNow,
                Status = "Planning"
            };

            // Calculer Takt Time automatiquement
            plan.CalculateTaktTime();
            // TaktTime = (20h × 3600s) / 1000 pièces = 72 secondes/pièce

            _plans.Add(plan);
            return Result<ProductionPlan>.Ok(plan, "Production plan created successfully");
        }

        /// <summary>
        /// Valide si le plan est réalisable
        /// </summary>
        public PlanValidation ValidatePlan(ProductionPlan plan, ProductionLine line)
        {
            var isValid = true;
            var warnings = new List<string>();

            // Comparer Takt Time vs Cadence cible
            var requiredCadence = plan.RequiredCadence;
            var lineCadence = line.TargetCadence;

            if (requiredCadence > lineCadence)
            {
                isValid = false;
                warnings.Add($"Required cadence ({requiredCadence:F1} p/h) exceeds line capacity ({lineCadence} p/h)");
            }

            // Vérifier si délai est réaliste
            if (plan.TimeRemaining.TotalHours < 1)
            {
                warnings.Add("Deadline is very close. Production may not complete in time");
            }

            return new PlanValidation
            {
                IsValid = isValid,
                Warnings = warnings
            };
        }
    }
}
