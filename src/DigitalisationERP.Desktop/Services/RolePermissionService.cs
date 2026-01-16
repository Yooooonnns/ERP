using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalisationERP.Desktop.Services
{
    /// <summary>
    /// Service de gestion des rôles et permissions
    /// Contrôle l'accès aux différentes pages et fonctionnalités selon la nouvelle logique
    /// </summary>
    public class RolePermissionService
    {
        // Énumération des rôles avec hiérarchie complète par département
        public enum UserRole
        {
            SAP_ALL,        // Admin: accès complet partout
            S_USER,         // S-User: accès admin (desktop)
            // Production Department
            Z_PROD_MANAGER,     // Production Manager: gestion complète production
            Z_PROD_PLANNER,     // Production Planner: planification production
            Z_PROD_OPERATOR,    // Production Operator: exécution production
            // Maintenance Department  
            Z_MAINT_MANAGER,    // Maintenance Manager: gestion complète maintenance
            Z_MAINT_PLANNER,    // Maintenance Planner: planification maintenance
            Z_MAINT_TECH,       // Maintenance Technician: exécution maintenance
            // Warehouse Department
            Z_WM_MANAGER,       // Warehouse Manager: gestion complète entrepôt
            Z_WM_CLERK,         // Warehouse Clerk: opérations entrepôt
            Z_MM_BUYER,         // Material Buyer: achats
            // Quality Department
            Z_QM_MANAGER,       // Quality Manager: gestion qualité
            Z_QM_INSPECTOR,     // Quality Inspector: contrôles qualité
            // Robotics & IoT
            Z_ROBOT_ADMIN,      // Robot Administrator
            Z_ROBOT_OPERATOR,   // Robot Operator
            Z_IOT_ADMIN,        // IoT Administrator
            Z_IOT_MONITOR       // IoT Monitor
        }

        private UserRole _currentRole;
        private string _currentUserId;

        public RolePermissionService(string role, string userId = "")
        {
            _currentUserId = userId;
            _currentRole = ParseRole(role);
        }

        /// <summary>
        /// Convertit une string en enum UserRole
        /// </summary>
        private UserRole ParseRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return UserRole.Z_PROD_OPERATOR;

            // Normalize common variations coming from backend/AD (e.g., "S-user", "Z-PROD-MANAGER").
            var normalized = role.Trim()
                .ToUpperInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');

            return normalized switch
            {
                "SAP_ALL" => UserRole.SAP_ALL,
                "S_USER" => UserRole.S_USER,
                // Production Department
                "Z_PROD_MANAGER" => UserRole.Z_PROD_MANAGER,
                "Z_PROD_PLANNER" => UserRole.Z_PROD_PLANNER,
                "Z_PROD_OPERATOR" => UserRole.Z_PROD_OPERATOR,
                // Maintenance Department
                "Z_MAINT_MANAGER" => UserRole.Z_MAINT_MANAGER,
                "Z_MAINT_PLANNER" => UserRole.Z_MAINT_PLANNER,
                "Z_MAINT_TECH" => UserRole.Z_MAINT_TECH,
                // Warehouse Department
                "Z_WM_MANAGER" => UserRole.Z_WM_MANAGER,
                "Z_WM_CLERK" => UserRole.Z_WM_CLERK,
                "Z_MM_BUYER" => UserRole.Z_MM_BUYER,
                // Quality Department
                "Z_QM_MANAGER" => UserRole.Z_QM_MANAGER,
                "Z_QM_INSPECTOR" => UserRole.Z_QM_INSPECTOR,
                // Robotics & IoT
                "Z_ROBOT_ADMIN" => UserRole.Z_ROBOT_ADMIN,
                "Z_ROBOT_OPERATOR" => UserRole.Z_ROBOT_OPERATOR,
                "Z_IOT_ADMIN" => UserRole.Z_IOT_ADMIN,
                "Z_IOT_MONITOR" => UserRole.Z_IOT_MONITOR,
                // Legacy compatibility
                "Z_MANAGER" => UserRole.Z_PROD_MANAGER,
                "Z_PROD" => UserRole.Z_PROD_OPERATOR,
                "Z_MAINTENANCE" => UserRole.Z_MAINT_TECH,
                _ => UserRole.Z_PROD_OPERATOR // Fallback
            };
        }

        /// <summary>
        /// Vérifie si l'utilisateur a accès à une page spécifique selon nouvelle logique
        /// </summary>
        public bool CanAccessPage(string pageName)
        {
            // Desktop baseline: everyone can see the main dashboard.
            if (string.Equals(pageName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Meetings: everyone can view/participate; creation is role-gated elsewhere.
            if (string.Equals(pageName, "Meetings", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Account management center should be restricted.
            if (string.Equals(pageName, "UsersManagement", StringComparison.OrdinalIgnoreCase))
            {
                // S_USER always allowed.
                if (_currentRole == UserRole.S_USER) return true;

                // Maintenance IT: use maintenance tech role + userId/email contains "it".
                if (_currentRole == UserRole.Z_MAINT_TECH &&
                    _currentUserId.Contains("it", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }

            return _currentRole switch
            {
                // S_USER: Accès à tout (desktop-only)
                UserRole.S_USER => true,
                
                // SAP_ALL (Admin): Accès complet
                UserRole.SAP_ALL => true,
                
                // Production Department
                UserRole.Z_PROD_MANAGER => pageName switch
                {
                    "Production" => true,
                    "Maintenance" => true, // guest visualization
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },
                UserRole.Z_PROD_PLANNER => pageName switch
                {
                    "Production" => true,
                    "Maintenance" => true, // guest visualization
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },
                UserRole.Z_PROD_OPERATOR => pageName switch
                {
                    "Production" => true,
                    "Maintenance" => true, // guest visualization
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "TaskBoard" => true,
                    "Email" => true,
                    _ => false
                },

                // Maintenance Department
                UserRole.Z_MAINT_MANAGER => pageName switch
                {
                    "Maintenance" => true,
                    "IotConsole" => true,
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },
                UserRole.Z_MAINT_PLANNER => pageName switch
                {
                    "Maintenance" => true,
                    "IotConsole" => true,
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },

                // Warehouse Department
                UserRole.Z_WM_MANAGER => pageName switch
                {
                    "Inventory" => true,
                    "Maintenance" => true, // guest visualization
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },
                UserRole.Z_WM_CLERK => pageName switch
                {
                    "Inventory" => true,
                    "Maintenance" => true, // guest visualization
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "TaskBoard" => true,
                    "Email" => true,
                    _ => false
                },
                UserRole.Z_MM_BUYER => pageName switch
                {
                    "Inventory" => true,
                    "Maintenance" => true, // guest visualization
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Email" => true,
                    "Reports" => true,
                    _ => false
                },

                // Quality Department
                UserRole.Z_QM_MANAGER => pageName switch
                {
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Maintenance" => true, // guest visualization
                    "Planning" => true,
                    "ShiftPlanner" => true,
                    "Reports" => true,
                    "Email" => true,
                    "TaskBoard" => true,
                    "Feedback" => true,
                    _ => false
                },
                UserRole.Z_QM_INSPECTOR => pageName switch
                {
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "Maintenance" => true, // guest visualization
                    "Email" => true,
                    _ => false
                },

                // Robotics & IoT
                UserRole.Z_ROBOT_ADMIN => pageName switch
                {
                    "IotConsole" => true,
                    "MyTasks" => true,
                    "Email" => true,
                    "Configuration" => true,
                    "Reports" => true,
                    _ => false
                },
                UserRole.Z_ROBOT_OPERATOR => pageName switch
                {
                    "IotConsole" => true,
                    "MyTasks" => true,
                    "Email" => true,
                    _ => false
                },
                UserRole.Z_IOT_ADMIN => pageName switch
                {
                    "IotConsole" => true,
                    "MyTasks" => true,
                    "Email" => true,
                    "Configuration" => true,
                    "Reports" => true,
                    _ => false
                },
                UserRole.Z_IOT_MONITOR => pageName switch
                {
                    "IotConsole" => true,
                    "MyTasks" => true,
                    "Email" => true,
                    _ => false
                },
                
                // Z_MAINT_TECH: Desktop consoles temps réel
                UserRole.Z_MAINT_TECH => pageName switch
                {
                    // In MaintenanceModeWindow the routing differs, but keep minimal desktop access here.
                    "Maintenance" => true,
                    "IotConsole" => true,
                    "MyTasks" => true,
                    "MySchedule" => true,
                    "TaskBoard" => true,
                    "Email" => true,
                    _ => false
                },
                
                _ => false
            };
        }

        /// <summary>
        /// Accès selon département pour managers
        /// </summary>
        private bool GetDepartmentAccess(string department)
        {
            // TODO: Récupérer le département du manager depuis la DB
            // Pour l'instant, on assume accès selon email/userId
            return department switch
            {
                "Production" => _currentUserId.Contains("prod") || _currentUserId.Contains("manager"),
                "Warehouse" => _currentUserId.Contains("warehouse") || _currentUserId.Contains("manager"),
                "Maintenance" => _currentUserId.Contains("maintenance"),
                _ => false
            };
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut juste visualiser (sans modifier)
        /// </summary>
        private bool CanVisualizeOnly(string pageName)
        {
            return pageName switch
            {
                "Dashboard" => true,
                "Analytics" => true,
                "Reports" => true,
                _ => false
            };
        }

        /// <summary>
        /// Obtient la page principale à afficher selon le rôle
        /// </summary>
        public string GetMainPageName()
        {
            return _currentRole switch
            {
                UserRole.S_USER => "AdminDashboard",
                UserRole.SAP_ALL => "DirectionDashboard",
                UserRole.Z_PROD_MANAGER => "TeamManagementDashboard",
                UserRole.Z_PROD_PLANNER => "TeamLeaderDashboard",
                UserRole.Z_PROD_OPERATOR => "ProductionOperatorDashboard",
                UserRole.Z_WM_CLERK => "WarehouseOperatorDashboard",
                UserRole.Z_MAINT_TECH => "RealtimeConsoleDashboard", // Console temps réel
                _ => "DefaultDashboard"
            };
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut effectuer une action spécifique
        /// </summary>
        public bool CanPerformAction(string action)
        {
            return _currentRole switch
            {
                // S_USER: Accès à tout + gestion comptes
                UserRole.S_USER => true,
                
                // SAP_ALL: Messages + réunions + visualisation
                UserRole.SAP_ALL => action switch
                {
                    "SendMessages" => true,
                    "CreateMeetings" => true,
                    "SelectParticipants" => true,
                    "ViewAllDashboards" => true,
                    "ViewAnalytics" => true,
                    _ => false
                },
                
                // Z_PROD_MANAGER: Assigner tâches + gestion équipe
                UserRole.Z_PROD_MANAGER => action switch
                {
                    "AssignTasks" => true,
                    "CreateTeamMeetings" => true,
                    "ManageTeam" => true,
                    "ManageShifts" => true,
                    "ScheduleEmployees" => true,
                    _ => false
                },
                
                // Z_PROD_PLANNER: Tâches + réunions immédiates + rapports
                UserRole.Z_PROD_PLANNER => action switch
                {
                    "DistributeTasks" => true,
                    "CreateImmediateMeetings" => true,
                    "CommunicateWithTeams" => true,
                    "WriteReports" => true,
                    "SubmitReportsToManager" => true,
                    _ => false
                },
                
                // Z_PROD_OPERATOR: Status tâches + demandes maintenance
                UserRole.Z_PROD_OPERATOR => action switch
                {
                    "UpdateTaskStatus" => true,
                    "MarkTaskCompleted" => true,
                    "MarkTaskInProgress" => true,
                    "MarkTaskIncomplete" => true,
                    "ReportProblem" => true,
                    "RequestMaintenance" => true,
                    _ => false
                },

                // Z_MAINT_MANAGER: gestion équipe + planification
                UserRole.Z_MAINT_MANAGER => action switch
                {
                    "AssignTasks" => true,
                    "ManageTeam" => true,
                    "ManageShifts" => true,
                    "ScheduleEmployees" => true,
                    _ => false
                },

                // Z_MAINT_PLANNER: planification + rapports
                UserRole.Z_MAINT_PLANNER => action switch
                {
                    "DistributeTasks" => true,
                    "ManageShifts" => true,
                    "ScheduleEmployees" => true,
                    "WriteReports" => true,
                    "SubmitReportsToManager" => true,
                    _ => false
                },
                
                // Z_WM_CLERK: Status tâches + demandes maintenance  
                UserRole.Z_WM_CLERK => action switch
                {
                    "UpdateTaskStatus" => true,
                    "MarkTaskCompleted" => true,
                    "MarkTaskInProgress" => true,
                    "MarkTaskIncomplete" => true,
                    "ReportProblem" => true,
                    "RequestMaintenance" => true,
                    _ => false
                },
                
                // Z_MAINT_TECH: Consoles temps réel (desktop)
                UserRole.Z_MAINT_TECH => action switch
                {
                    "AccessRealtimeConsoles" => true,
                    "MonitorSystem" => true,
                    "HandleMaintenanceRequests" => true,
                    "AccessDatabaseConsole" => true,
                    "AccessApiConsole" => true,
                    "AccessGitHubIntegration" => true,
                    "ViewBugReports" => true,
                    "ManageAccountIssues" => true,
                    _ => false
                },
                
                _ => false
            };
        }

        /// <summary>
        /// Desktop-only: l'application ne route pas vers des pages web.
        /// Méthode conservée pour compatibilité (retourne toujours false).
        /// </summary>
        public bool CanAccessWebPages()
        {
            return false;
        }

        /// <summary>
        /// Desktop-only: aucune URL web.
        /// Méthode conservée pour compatibilité (retourne string.Empty).
        /// </summary>
        public string GetWebPageUrl()
        {
            return string.Empty;
        }

        /// <summary>
        /// Obtient la liste des pages disponibles pour l'utilisateur
        /// </summary>
        public List<MenuItemData> GetAvailableMenuItems()
        {
            return _currentRole switch
            {
                UserRole.S_USER => new List<MenuItemData>
                {
                    new("Dashboard Admin", "AdminDashboard", "fas fa-chart-bar"),
                    new("Gestion des Utilisateurs", "UsersManagement", "fas fa-users"),
                    new("Rapports", "Reports", "fas fa-chart-line"),
                    new("Paramètres", "Settings", "fas fa-cog"),
                    new("Mode Développement", "DevMode", "fas fa-code")
                },
                UserRole.SAP_ALL => new List<MenuItemData>
                {
                    new("Dashboard Admin", "AdminDashboard", "fas fa-chart-bar"),
                    new("Gestion des Utilisateurs", "UsersManagement", "fas fa-users"),
                    new("Rapports", "Reports", "fas fa-chart-line"),
                    new("Paramètres", "Settings", "fas fa-cog")
                },
                // Production Department
                UserRole.Z_PROD_MANAGER => new List<MenuItemData>
                {
                    new("Dashboard Production", "ProductionDashboard", "fas fa-industry"),
                    new("Dashboard Équipe", "TeamDashboard", "fas fa-users-cog"),
                    new("Planning Production", "Planning", "fas fa-calendar-alt"),
                    new("Rapports Production", "Reports", "fas fa-chart-line"),
                    new("Messagerie", "Messaging", "fas fa-envelope")
                },
                UserRole.Z_PROD_PLANNER => new List<MenuItemData>
                {
                    new("Dashboard Production", "ProductionDashboard", "fas fa-industry"),
                    new("Planning Production", "Planning", "fas fa-calendar-alt"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks"),
                    new("Rapports", "Reports", "fas fa-chart-line")
                },
                UserRole.Z_PROD_OPERATOR => new List<MenuItemData>
                {
                    new("Chaîne de Production", "ProductionDashboard", "fas fa-industry"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                // Maintenance Department
                UserRole.Z_MAINT_MANAGER => new List<MenuItemData>
                {
                    new("Dashboard Maintenance", "MaintenanceDashboard", "fas fa-wrench"),
                    new("Dashboard Équipe", "TeamDashboard", "fas fa-users-cog"),
                    new("Planning Maintenance", "Planning", "fas fa-calendar-alt"),
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Rapports", "Reports", "fas fa-chart-line"),
                    new("Messagerie", "Messaging", "fas fa-envelope")
                },
                UserRole.Z_MAINT_PLANNER => new List<MenuItemData>
                {
                    new("Dashboard Maintenance", "MaintenanceDashboard", "fas fa-wrench"),
                    new("Planning Maintenance", "Planning", "fas fa-calendar-alt"),
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks"),
                    new("Rapports", "Reports", "fas fa-chart-line")
                },
                UserRole.Z_MAINT_TECH => new List<MenuItemData>
                {
                    new("Dashboard Maintenance", "MaintenanceDashboard", "fas fa-wrench"),
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                // Warehouse Department
                UserRole.Z_WM_MANAGER => new List<MenuItemData>
                {
                    new("Dashboard Entrepôt", "InventoryDashboard", "fas fa-warehouse"),
                    new("Dashboard Équipe", "TeamDashboard", "fas fa-users-cog"),
                    new("Planning Entrepôt", "Planning", "fas fa-calendar-alt"),
                    new("Rapports", "Reports", "fas fa-chart-line"),
                    new("Messagerie", "Messaging", "fas fa-envelope")
                },
                UserRole.Z_WM_CLERK => new List<MenuItemData>
                {
                    new("Vue Entrepôt", "InventoryDashboard", "fas fa-warehouse"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                UserRole.Z_MM_BUYER => new List<MenuItemData>
                {
                    new("Dashboard Achats", "InventoryDashboard", "fas fa-shopping-cart"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks"),
                    new("Rapports", "Reports", "fas fa-chart-line")
                },
                // Quality Department
                UserRole.Z_QM_MANAGER => new List<MenuItemData>
                {
                    new("Dashboard Qualité", "QualityDashboard", "fas fa-certificate"),
                    new("Dashboard Équipe", "TeamDashboard", "fas fa-users-cog"),
                    new("Planning Qualité", "Planning", "fas fa-calendar-alt"),
                    new("Rapports", "Reports", "fas fa-chart-line"),
                    new("Messagerie", "Messaging", "fas fa-envelope")
                },
                UserRole.Z_QM_INSPECTOR => new List<MenuItemData>
                {
                    new("Dashboard Qualité", "QualityDashboard", "fas fa-certificate"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                // Robotics & IoT
                UserRole.Z_ROBOT_ADMIN => new List<MenuItemData>
                {
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Configuration", "Configuration", "fas fa-cog"),
                    new("Rapports", "Reports", "fas fa-chart-line")
                },
                UserRole.Z_ROBOT_OPERATOR => new List<MenuItemData>
                {
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                UserRole.Z_IOT_ADMIN => new List<MenuItemData>
                {
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Configuration", "Configuration", "fas fa-cog"),
                    new("Rapports", "Reports", "fas fa-chart-line")
                },
                UserRole.Z_IOT_MONITOR => new List<MenuItemData>
                {
                    new("Console IoT", "IotConsole", "fas fa-terminal"),
                    new("Mes Tâches", "MyTasks", "fas fa-tasks")
                },
                _ => new List<MenuItemData>()
            };
        }

        /// <summary>
        /// Obtient le nom complet du rôle en français
        /// </summary>
        public string GetRoleDisplayName()
        {
            return _currentRole switch
            {
                UserRole.S_USER => "Super Admin Développeur",
                UserRole.SAP_ALL => "Administrateur Système",
                // Production Department
                UserRole.Z_PROD_MANAGER => "Manager Production",
                UserRole.Z_PROD_PLANNER => "Responsable Production",
                UserRole.Z_PROD_OPERATOR => "Opérateur Production",
                // Maintenance Department
                UserRole.Z_MAINT_MANAGER => "Manager Maintenance",
                UserRole.Z_MAINT_PLANNER => "Responsable Maintenance",
                UserRole.Z_MAINT_TECH => "Technicien Maintenance",
                // Warehouse Department
                UserRole.Z_WM_MANAGER => "Manager Entrepôt",
                UserRole.Z_WM_CLERK => "Opérateur Entrepôt",
                UserRole.Z_MM_BUYER => "Acheteur",
                // Quality Department
                UserRole.Z_QM_MANAGER => "Manager Qualité",
                UserRole.Z_QM_INSPECTOR => "Inspecteur Qualité",
                // Robotics & IoT
                UserRole.Z_ROBOT_ADMIN => "Administrateur Robotique",
                UserRole.Z_ROBOT_OPERATOR => "Opérateur Robotique",
                UserRole.Z_IOT_ADMIN => "Administrateur IoT",
                UserRole.Z_IOT_MONITOR => "Superviseur IoT",
                _ => "Utilisateur"
            };
        }

        /// <summary>
        /// Obtient la couleur du badge pour le rôle
        /// </summary>
        public string GetRoleColor()
        {
            return _currentRole switch
            {
                UserRole.S_USER => "#3b82f6",        // Bleu
                UserRole.SAP_ALL => "#8b5cf6",       // Violet
                // Production Department
                UserRole.Z_PROD_MANAGER => "#10b981", // Vert
                UserRole.Z_PROD_PLANNER => "#14b8a6", // Teal
                UserRole.Z_PROD_OPERATOR => "#f59e0b", // Orange
                // Maintenance Department
                UserRole.Z_MAINT_MANAGER => "#06b6d4", // Cyan
                UserRole.Z_MAINT_PLANNER => "#0891b2", // Cyan foncé
                UserRole.Z_MAINT_TECH => "#0e7490",   // Cyan très foncé
                // Warehouse Department
                UserRole.Z_WM_MANAGER => "#ec4899",   // Rose
                UserRole.Z_WM_CLERK => "#db2777",     // Rose foncé
                UserRole.Z_MM_BUYER => "#be185d",     // Rose très foncé
                // Quality Department
                UserRole.Z_QM_MANAGER => "#7c3aed",   // Violet
                UserRole.Z_QM_INSPECTOR => "#6d28d9", // Violet foncé
                // Robotics & IoT
                UserRole.Z_ROBOT_ADMIN => "#dc2626",  // Rouge
                UserRole.Z_ROBOT_OPERATOR => "#b91c1c", // Rouge foncé
                UserRole.Z_IOT_ADMIN => "#059669",    // Émeraude
                UserRole.Z_IOT_MONITOR => "#047857",  // Émeraude foncé
                _ => "#6b7280"                       // Gris
            };
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut accéder aux consoles desktop en temps réel
        /// </summary>
        public bool CanAccessRealtimeConsoles()
        {
            return _currentRole switch
            {
                UserRole.Z_MAINT_MANAGER => true,
                UserRole.Z_MAINT_PLANNER => true,
                UserRole.Z_MAINT_TECH => true,
                UserRole.Z_ROBOT_ADMIN => true,
                UserRole.Z_ROBOT_OPERATOR => true,
                UserRole.Z_IOT_ADMIN => true,
                UserRole.Z_IOT_MONITOR => true,
                _ => false
            };
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut router les demandes de maintenance
        /// </summary>
        public bool CanReceiveMaintenanceRequests()
        {
            return _currentRole == UserRole.Z_MAINT_TECH;
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut créer des réunions avec participants
        /// </summary>
        public bool CanCreateMeetingsWithParticipants()
        {
            return _currentRole == UserRole.SAP_ALL;
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut créer des réunions d'équipe
        /// </summary>
        public bool CanCreateTeamMeetings()
        {
            return _currentRole == UserRole.Z_PROD_MANAGER;
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut demander des réunions immédiates
        /// </summary>
        public bool CanRequestImmediateMeetings()
        {
            return _currentRole == UserRole.Z_PROD_PLANNER;
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut accéder à la page web admin
        /// </summary>
        public bool CanAccessWebAdmin()
        {
            return false;
        }

        /// <summary>
        /// Vérifie si l'utilisateur a accès aux outils de développement web
        /// </summary>
        public bool CanAccessWebDevTools()
        {
            return false;
        }

        /// <summary>
        /// Vérifie si l'utilisateur peut utiliser le mode développement
        /// </summary>
        public bool CanUseDeveloperMode()
        {
            return _currentRole == UserRole.S_USER;
        }

        /// <summary>
        /// Classe helper pour les items du menu
        /// </summary>
        public class MenuItemData
        {
            public string Label { get; set; }
            public string PageName { get; set; }
            public string Icon { get; set; }

            public MenuItemData(string label, string pageName, string icon)
            {
                Label = label;
                PageName = pageName;
                Icon = icon;
            }
        }

        // Propriétés publiques
        public UserRole CurrentRole => _currentRole;
        public string CurrentUserId => _currentUserId;
    }
}
