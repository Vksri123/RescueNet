using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResuceNet.Data;
using ResuceNet.Services;
using ResuceNet.ViewModels;

namespace ResuceNet.Controllers
{
    [Authorize(Roles = "RescueTeam,Admin")]
    public class RescueTeamController : Controller
    {
        private readonly IEmergencyService _emergencyService;
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public RescueTeamController(
            IEmergencyService emergencyService,
            ApplicationDbContext context,
            IServiceScopeFactory scopeFactory)
        {
            _emergencyService = emergencyService;
            _context = context;
            _scopeFactory = scopeFactory;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var team = await _context.RescueTeams
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (team == null)
            {
                return NotFound("Rescue team profile not found.");
            }

            // Fetch active assignment
            var activeAssignment = await _context.EmergencyAssignments
                .Include(a => a.EmergencyRequest)
                .ThenInclude(r => r!.Citizen)
                .FirstOrDefaultAsync(a => a.RescueTeamId == team.Id && a.Status != "Completed" && a.Status != "Rejected");

            var history = new System.Collections.Generic.List<Models.EmergencyStatusHistory>();
            if (activeAssignment != null)
            {
                history = await _context.EmergencyStatusHistories
                    .Where(h => h.EmergencyRequestId == activeAssignment.EmergencyRequestId)
                    .OrderBy(h => h.ChangedAt)
                    .ToListAsync();
            }

            // Fetch list of unassigned emergencies that the team could self-assign or view
            var unassigned = await _context.EmergencyRequests
                .Where(r => r.Status == "Created" || r.Status == "AI Analyzed")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var viewModel = new RescueTeamDashboardViewModel
            {
                Team = team,
                ActiveAssignment = activeAssignment,
                ActiveEmergencyHistory = history,
                UnassignedEmergencies = unassigned
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Accept(int requestId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized();
            }

            var success = await _emergencyService.UpdateStatusAsync(
                requestId,
                "Rescue Team Accepted",
                "Rescue team accepted the dispatch and is preparing gear.",
                userId
            );

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int requestId, string status, string? notes)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized();
            }

            var success = await _emergencyService.UpdateStatusAsync(requestId, status, notes, userId);
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTeamStatus(string status)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized();
            }

            var team = await _context.RescueTeams.FirstOrDefaultAsync(t => t.UserId == userId);
            if (team != null)
            {
                await _emergencyService.UpdateTeamStatusAsync(team.Id, status);
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> StartSimulation(int requestId, int teamId)
        {
            // Fire-and-forget background movement task
            _ = SimulateTeamMovement(teamId, requestId);

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                userId = 0;
            }

            // Immediately mark status as 'On The Way'
            await _emergencyService.UpdateStatusAsync(
                requestId,
                "On The Way",
                "Rescue team is en route (live GPS simulation started).",
                userId > 0 ? userId : null
            );

            return RedirectToAction("Dashboard");
        }

        private async Task SimulateTeamMovement(int teamId, int requestId)
        {
            // Number of interpolation steps
            const int steps = 10;
            const int delayMs = 1500;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var emergencyService = scope.ServiceProvider.GetRequiredService<IEmergencyService>();

                var team = await context.RescueTeams.FindAsync(teamId);
                var request = await context.EmergencyRequests.FindAsync(requestId);

                if (team == null || request == null) return;

                decimal startLat = team.CurrentLatitude ?? 23.0225m;
                decimal startLon = team.CurrentLongitude ?? 72.5714m;

                decimal targetLat = request.Latitude;
                decimal targetLon = request.Longitude;

                for (int i = 1; i <= steps; i++)
                {
                    await Task.Delay(delayMs);

                    // Interpolate coordinates
                    decimal fraction = (decimal)i / steps;
                    decimal curLat = startLat + (targetLat - startLat) * fraction;
                    decimal curLon = startLon + (targetLon - startLon) * fraction;

                    // Update GPS coordinate and broadcast
                    await emergencyService.UpdateTeamLocationAsync(teamId, curLat, curLon, requestId);

                    // If we reached the target, automatically update status to "Rescue In Progress"
                    if (i == steps)
                    {
                        await Task.Delay(500);
                        await emergencyService.UpdateStatusAsync(
                            requestId,
                            "Rescue In Progress",
                            "Rescue team has arrived at the location. Rescue operations are now underway.",
                            null
                        );
                    }
                }
            }
        }
    }
}
