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

            // Trigger "AI Analyzed" and Auto-Dispatch simulation in the background
            _ = SimulateAIAnalysisAndDispatch(request.Id);

            return request;
        }

        private async Task SimulateAIAnalysisAndDispatch(int requestId)
        {
            // Wait 3 seconds to simulate AI analysis
            await Task.Delay(3000);

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var priorityService = scope.ServiceProvider.GetRequiredService<IPriorityService>();
                var teamAssignmentService = scope.ServiceProvider.GetRequiredService<ITeamAssignmentService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<EmergencyHub>>();

                var request = await context.EmergencyRequests.FindAsync(requestId);
                if (request == null) return;

                // 1. Analyze priority
                var (priority, score) = await priorityService.AnalyzePriorityAsync(request.Description, request.EmergencyType);
                
                request.PriorityLevel = priority;
                request.RiskScore = score;
                request.Status = "AI Analyzed";
                request.UpdatedAt = DateTime.UtcNow;

                var history = new EmergencyStatusHistory
                {
                    EmergencyRequestId = request.Id,
                    Status = "AI Analyzed",
                    Notes = $"AI Analysis complete. Priority: {priority}, Risk Score: {score}/100.",
                    ChangedAt = DateTime.UtcNow
                };

                context.EmergencyStatusHistories.Add(history);
                await context.SaveChangesAsync();

                // Broadcast AI updates
                await hubContext.Clients.All.SendAsync("StatusUpdate", request.Id, "AI Analyzed", $"Priority set to {priority}. Risk Score: {score}.");

                // 2. Find nearest team and assign automatically (if Critical or High, otherwise wait for manual dispatch)
                // Let's automatically dispatch if it's High/Critical and an available team is close.
                // For demonstration, let's auto-dispatch ALL emergencies if an available team is found.
                var nearestTeam = await teamAssignmentService.FindNearestAvailableTeamAsync(request.Latitude, request.Longitude);
                if (nearestTeam != null)
                {
                    // Wait another 2 seconds to simulate automatic team dispatching
                    await Task.Delay(2000);

                    // Re-fetch request & team in scope db context
                    var reqToAssign = await context.EmergencyRequests.FindAsync(requestId);
                    var teamToAssign = await context.RescueTeams.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == nearestTeam.Id);

                    if (reqToAssign != null && teamToAssign != null && teamToAssign.Status == "Available")
                    {
                        reqToAssign.Status = "Assigned";
                        reqToAssign.UpdatedAt = DateTime.UtcNow;

                        var assignment = new EmergencyAssignment
                        {
                            EmergencyRequestId = reqToAssign.Id,
                            RescueTeamId = teamToAssign.Id,
                            Status = "Assigned",
                            AssignedAt = DateTime.UtcNow
                        };

                        teamToAssign.Status = "Busy";

                        var assignHistory = new EmergencyStatusHistory
                        {
                            EmergencyRequestId = reqToAssign.Id,
                            Status = "Assigned",
                            Notes = $"System auto-assigned rescue team '{teamToAssign.TeamName}' (Distance: {teamAssignmentService.CalculateDistance(reqToAssign.Latitude, reqToAssign.Longitude, teamToAssign.CurrentLatitude!.Value, teamToAssign.CurrentLongitude!.Value):F2} km).",
                            ChangedAt = DateTime.UtcNow
                        };

                        context.EmergencyAssignments.Add(assignment);
                        context.EmergencyStatusHistories.Add(assignHistory);
                        await context.SaveChangesAsync();

                        await hubContext.Clients.All.SendAsync("StatusUpdate", reqToAssign.Id, "Assigned", $"Rescue team '{teamToAssign.TeamName}' assigned to this incident.");
                    }
                }
            }
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
                        team.Status = "Available"; // Release team
                    }
                }
            }
            else
            {
                var activeAssignment = await _context.EmergencyAssignments
                    .FirstOrDefaultAsync(a => a.EmergencyRequestId == requestId && a.Status != "Completed" && a.Status != "Rejected");

                if (activeAssignment != null)
                {
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

            team.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
