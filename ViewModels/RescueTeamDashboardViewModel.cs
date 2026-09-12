using System.Collections.Generic;
using ResuceNet.Models;

namespace ResuceNet.ViewModels
{
    public class RescueTeamDashboardViewModel
    {
        public RescueTeam Team { get; set; } = new();
        public EmergencyAssignment? ActiveAssignment { get; set; }
        public List<EmergencyStatusHistory> ActiveEmergencyHistory { get; set; } = new();
        public List<EmergencyRequest> UnassignedEmergencies { get; set; } = new();
    }
}
