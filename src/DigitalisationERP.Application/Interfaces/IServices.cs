using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DigitalisationERP.Domain;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Interfaces
{
    /// <summary>
    /// Service d'authentification pour inscription/connexion
    /// </summary>
    public interface IAuthenticationService
    {
        Task<Result<User>> RegisterAsync(string email, string password, string firstName, string lastName);
        Task<Result<User>> LoginAsync(string email, string password);
        Task<bool> ValidateTokenAsync(string token);
    }

    /// <summary>
    /// Service de gestion des lignes de production
    /// </summary>
    public interface IProductionLineService
    {
        Task<Result<ProductionLine>> CreateLineAsync(int userId, string lineName, string description,
            double minAvailability, double minPerformance, double minQuality);
        Task<ProductionLine?> GetUserLineAsync(int userId);
        Task<Result<ProductionPost>> AddPostAsync(int lineId, string postCode, string postName, int position,
            string postType, int targetCadence, int maxCapacity);
        Task<List<ProductionPost>> GetLinePostsAsync(int lineId);
    }

    /// <summary>
    /// Service de planification production avec calcul Takt Time
    /// </summary>
    public interface IProductionPlanningService
    {
        Task<Result<ProductionPlan>> CreatePlanAsync(int lineId, int quantityToProduce, DateTime targetDeadline);
        PlanValidation ValidatePlan(ProductionPlan plan, ProductionLine line);
    }

    /// <summary>
    /// Service de calcul OEE en temps réel
    /// </summary>
    public interface IOEECalculationService
    {
        OEEMetrics CalculateOEE(int plannedMinutes, int actualRunTime, int idleTime,
            int producedUnits, int expectedUnits, int defectiveUnits);
        string GetOEEHealthColor(double oeeValue, double minOEE);
    }

    /// <summary>
    /// Service de gestion des alertes
    /// </summary>
    public interface IAlertService
    {
        Task<List<Alert>> CheckAndCreateAlertsAsync(ProductionLine line, OEEMetrics metrics,
            List<ProductionPost> posts);
        Task<List<Alert>> GetActiveAlertsAsync(int lineId);
    }

    /// <summary>
    /// Validation du plan de production
    /// </summary>
    public class PlanValidation
    {
        public bool IsValid { get; set; } = true;
        public List<string> Warnings { get; set; } = new();
    }
}
