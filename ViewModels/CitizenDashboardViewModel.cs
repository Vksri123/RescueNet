using System.Collections.Generic;
using ResuceNet.Models;

namespace ResuceNet.ViewModels
{
    public class CitizenRequestItem
    {
        public EmergencyRequest Request { get; set; } = null!;
        public EmergencyAssignment? Assignment { get; set; }
        public List<EmergencyStatusHistory> Histories { get; set; } = new();
    }

    public class CitizenDashboardViewModel
    {
        public User Citizen { get; set; } = null!;
        public int TotalRequests { get; set; }
        public int ActiveRequestsCount { get; set; }
        public int InProgressRequestsCount { get; set; }
        public int ResolvedRequestsCount { get; set; }

        public List<CitizenRequestItem> ActiveRequests { get; set; } = new();
        public List<CitizenRequestItem> CompletedRequests { get; set; } = new();
    }
}
