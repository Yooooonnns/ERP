using System;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Alerte générée automatiquement en fonction des seuils
    /// </summary>
    public class Alert
    {
        public int AlertId { get; set; }
        public int LineId { get; set; }
        public int? PostId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Contenu alerte
        public string AlertType { get; set; } = "";        // "OEE_CRITICAL" "STOCK_LOW" etc.
        public string Severity { get; set; } = "";         // "Critical" "Warning" "Info"
        public string Message { get; set; } = "";          // Description lisible
        public string? RecommendedAction { get; set; }     // Action suggérée

        // État
        public bool IsAcknowledged { get; set; } = false;
        public DateTime? AcknowledgedDate { get; set; }

        // Relations
        public virtual ProductionLine Line { get; set; } = null!;
        public virtual ProductionPost? Post { get; set; }

        // Propriétés calculées
        public string SeverityIcon
        {
            get => Severity switch
            {
                "Critical" => "🔴",
                "Warning" => "🟠",
                "Alert" => "🟡",
                "Info" => "🟢",
                _ => "⚪"
            };
        }

        public string GetDisplayText() => $"[{SeverityIcon} {Severity}] {Message}";
    }
}
