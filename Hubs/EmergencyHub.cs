using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ResuceNet.Hubs
{
    public class EmergencyHub : Hub
    {
        // Broadcasts to all users (Admins see live updates, Teams see nearby, Citizens see tracker updates)
        public async Task BroadcastNewEmergency(int id, string type, string description, decimal latitude, decimal longitude, string priority, int riskScore)
        {
            await Clients.All.SendAsync("NewEmergency", id, type, description, latitude, longitude, priority, riskScore);
        }

        public async Task BroadcastStatusUpdate(int id, string status, string notes)
        {
            await Clients.All.SendAsync("StatusUpdate", id, status, notes);
        }

        public async Task BroadcastTeamLocationUpdate(int teamId, int requestId, decimal latitude, decimal longitude, string status)
        {
            await Clients.All.SendAsync("TeamLocationUpdate", teamId, requestId, latitude, longitude, status);
        }

        public async Task JoinEmergencyGroup(string requestId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Emergency_{requestId}");
        }

        public async Task BroadcastSMSAlert(string message, string targetRole, string sentAt)
        {
            await Clients.All.SendAsync("SMSAlertReceived", message, targetRole, sentAt);
        }
    }
}
