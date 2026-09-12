using System.Collections.Generic;
using ResuceNet.Models;

namespace ResuceNet.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalActiveEmergencies { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }

        public List<EmergencyRequest> ActiveEmergencies { get; set; } = new();
        public List<RescueTeam> RescueTeams { get; set; } = new();

        // Chart analytical data (simple counts by category)
        public Dictionary<string, int> EmergencyTypeCounts { get; set; } = new();
        public Dictionary<string, int> MonthlyTrends { get; set; } = new();
    }
}
