using DigitalisationERP.Domain.Entities;

namespace DigitalisationERP.Desktop.Models
{
    /// <summary>
    /// Gère la session utilisateur courante dans l'application
    /// </summary>
    public static class AppSession
    {
        public static User? CurrentUser { get; set; }
        public static ProductionLine? CurrentLine { get; set; }
        public static ProductionPlan? CurrentPlan { get; set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Logout()
        {
            CurrentUser = null;
            CurrentLine = null;
            CurrentPlan = null;
        }
    }
}
