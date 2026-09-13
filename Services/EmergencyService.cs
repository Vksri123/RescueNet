using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResuceNet.Data;
using ResuceNet.Hubs;
using ResuceNet.Models;

namespace ResuceNet.Services
{
    public class EmergencyService : IEmergencyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPriorityService _priorityService;
        private readonly ITeamAssignmentService _teamAssignmentService;
        private readonly IHubContext<EmergencyHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public EmergencyService(
            ApplicationDbContext context,
            IPriorityService priorityService,
            ITeamAssignmentService teamAssignmentService,
            IHubContext<EmergencyHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _priorityService = priorityService;
            _teamAssignmentService = teamAssignmentService;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        public async Task<EmergencyRequest> CreateEmergencyAsync(int citizenId, string type, string description, decimal latitude, decimal longitude, string? mediaUrl)
        {
            var request = new EmergencyRequest
            {
                CitizenId = citizenId,
                EmergencyType = type,
                Description = description,
                Latitude = latitude,
                Longitude = longitude,
                Status = "Created",
                PriorityLevel = "Medium", // Default before AI analysis
                RiskScore = 0,
                MediaUrl = mediaUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.EmergencyRequests.Add(request);
            await _context.SaveChangesAsync();

            // Add history record
            var history = new EmergencyStatusHistory
            {
                EmergencyRequestId = request.Id,
                Status = "Created",
                Notes = "Emergency request reported and logged in system.",
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = citizenId
            };
            _context.EmergencyStatusHistories.Add(history);
            await _context.SaveChangesAsync();

            // Broadcast "Created" to SignalR
            await _hubContext.Clients.All.SendAsync("NewEmergency", request.Id, request.EmergencyType, request.Description, request.Latitude, request.Longitude, request.PriorityLevel, request.RiskScore);

            return request;
        }

        public async Task<bool> AssignTeamAsync(int requestId, int teamId)
        {
            var request = await _context.EmergencyRequests.FindAsync(requestId);
            var team = await _context.RescueTeams.FindAsync(teamId);

            if (request == null || team == null || team.Status != "Available")
            {
                return false;
            }

            request.Status = "Assigned";
            request.UpdatedAt = DateTime.UtcNow;

            var assignment = new EmergencyAssignment
            {
                EmergencyRequestId = request.Id,
                RescueTeamId = team.Id,
                Status = "Assigned",
                AssignedAt = DateTime.UtcNow
            };

            team.Status = "Busy";

            var history = new EmergencyStatusHistory
            {
                EmergencyRequestId = request.Id,
                Status = "Assigned",
                Notes = $"Rescue team '{team.TeamName}' manually assigned by Administrator.",
                ChangedAt = DateTime.UtcNow
            };

            _context.EmergencyAssignments.Add(assignment);
            _context.EmergencyStatusHistories.Add(history);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("StatusUpdate", request.Id, "Assigned", $"Rescue team '{team.TeamName}' assigned by Admin.");
            await _hubContext.Clients.All.SendAsync("TeamAssigned", team.Id, request.Id, team.TeamName);
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int requestId, string status, string? notes, int? changedByUserId)
        {
            var request = await _context.EmergencyRequests.FindAsync(requestId);
            if (request == null) return false;

            request.Status = status;
            request.UpdatedAt = DateTime.UtcNow;

            var history = new EmergencyStatusHistory
            {
                EmergencyRequestId = request.Id,
                Status = status,
                Notes = notes ?? $"Status updated to {status}.",
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = changedByUserId
            };

            _context.EmergencyStatusHistories.Add(history);

            // Handle cascading status on assignments and team status
            if (status == "Resolved" || status == "Rejected")
            {
                var assignments = await _context.EmergencyAssignments
                    .Where(a => a.EmergencyRequestId == requestId && a.Status != "Completed" && a.Status != "Rejected")
                    .ToListAsync();

                foreach (var a in assignments)
                {
                    a.Status = status == "Resolved" ? "Completed" : "Rejected";
                    a.CompletedAt = DateTime.UtcNow;

                    var team = await _context.RescueTeams.FindAsync(a.RescueTeamId);
                    if (team != null)
                    {
                        var hasOtherActive = await _context.EmergencyAssignments
                            .AnyAsync(other => other.RescueTeamId == team.Id &&
                                               other.EmergencyRequestId != requestId &&
                                               other.Status != "Completed" &&
                                               other.Status != "Rejected");
                        if (!hasOtherActive)
                        {
                            team.Status = "Available"; // Release team
                        }
                    }
                }
            }
            else
            {
                var activeAssignment = await _context.EmergencyAssignments
                    .FirstOrDefaultAsync(a => a.EmergencyRequestId == requestId && a.Status != "Completed" && a.Status != "Rejected");

                if (activeAssignment != null)
                {
                    // Ensure the assigned team is explicitly marked as Busy
                    var team = await _context.RescueTeams.FindAsync(activeAssignment.RescueTeamId);
                    if (team != null && team.Status != "Busy")
                    {
                        team.Status = "Busy";
                    }

                    if (status == "Rescue Team Accepted")
                    {
                        activeAssignment.Status = "Accepted";
                        activeAssignment.AcceptedAt = DateTime.UtcNow;
                    }
                    else if (status == "On The Way")
                    {
                        activeAssignment.Status = "OnTheWay";
                    }
                    else if (status == "Rescue In Progress")
                    {
                        activeAssignment.Status = "InProgress";
                    }
                }
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("StatusUpdate", request.Id, status, notes ?? $"Status updated to {status}.");
            return true;
        }

        public async Task<bool> UpdateTeamLocationAsync(int teamId, decimal latitude, decimal longitude, int requestId = 0)
        {
            var team = await _context.RescueTeams.FindAsync(teamId);
            if (team == null) return false;

            team.CurrentLatitude = latitude;
            team.CurrentLongitude = longitude;
            await _context.SaveChangesAsync();

            // Broadcast team location change via SignalR
            await _hubContext.Clients.All.SendAsync("TeamLocationUpdate", teamId, requestId, latitude, longitude, team.Status);
            return true;
        }

        public async Task<bool> UpdateTeamStatusAsync(int teamId, string status)
        {
            var team = await _context.RescueTeams.FindAsync(teamId);
            if (team == null) return false;

            // Check if team has any ongoing active emergency operations
            var hasActiveAssignment = await _context.EmergencyAssignments
                .Include(a => a.EmergencyRequest)
                .AnyAsync(a => a.RescueTeamId == teamId &&
                               a.Status != "Completed" &&
                               a.Status != "Rejected" &&
                               a.EmergencyRequest != null &&
                               a.EmergencyRequest.Status != "Resolved" &&
                               a.EmergencyRequest.Status != "Rejected");

            if (hasActiveAssignment)
            {
                // Must remain Busy on operation
                team.Status = "Busy";
                await _context.SaveChangesAsync();
                return false;
            }

            team.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
