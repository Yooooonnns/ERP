using System;
using System.Collections.Generic;

namespace DigitalisationERP.Domain.Entities
{
    /// <summary>
    /// Utilisateur du système avec authentification
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Relations
        public virtual List<ProductionLine> ProductionLines { get; set; } = new();

        // Propriété calculée
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
