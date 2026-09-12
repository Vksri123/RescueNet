using System.Threading.Tasks;
using ResuceNet.Models;

namespace ResuceNet.Services
{
    public interface ITeamAssignmentService
    {
        Task<RescueTeam?> FindNearestAvailableTeamAsync(decimal latitude, decimal longitude);
        double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2);
    }
}
