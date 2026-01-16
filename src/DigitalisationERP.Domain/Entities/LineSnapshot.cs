namespace DigitalisationERP.Domain.Entities;

/// <summary>
/// Snapshot des données d'une ligne de production à un moment donné
/// </summary>
public class LineSnapshot
{
    public int LineId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? LineName { get; set; }
    
    // Données de production
    public List<SensorSnapshot> Sensors { get; set; } = [];
    public List<PostSnapshot> Posts { get; set; } = [];
    
    // Santé globale
    public double HealthScore { get; set; } // 0-100%
    public string? Status { get; set; } // Running, Idle, Error, etc.
    
    // Alertes
    public List<string> ActiveAlerts { get; set; } = [];
    
    // OEE
    public double OEEScore { get; set; } // 0-100%
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
}

/// <summary>
/// Snapshot de capteur
/// </summary>
public class SensorSnapshot
{
    public int SensorId { get; set; }
    public string? Name { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public bool IsAnomalous { get; set; }
    public DateTime ReadingTime { get; set; }
}

/// <summary>
/// Snapshot de poste de travail
/// </summary>
public class PostSnapshot
{
    public int PostId { get; set; }
    public string? PostName { get; set; }
    public int UnitsProduced { get; set; }
    public int DefectiveUnits { get; set; }
    public double Efficiency { get; set; } // %
    public string? Status { get; set; } // Running, Idle, Maintenance, etc.
    public DateTime LastUpdate { get; set; }
}
