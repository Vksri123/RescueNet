using System.Threading.Tasks;
using ResuceNet.Models;

namespace ResuceNet.Services
{
    public interface IEmergencyService
    {
        Task<EmergencyRequest> CreateEmergencyAsync(int citizenId, string type, string description, decimal latitude, decimal longitude, string? mediaUrl);
        Task<bool> AssignTeamAsync(int requestId, int teamId);
        Task<bool> UpdateStatusAsync(int requestId, string status, string? notes, int? changedByUserId);
        Task<bool> UpdateTeamLocationAsync(int teamId, decimal latitude, decimal longitude, int requestId = 0);
        Task<bool> UpdateTeamStatusAsync(int teamId, string status);
    }
}
