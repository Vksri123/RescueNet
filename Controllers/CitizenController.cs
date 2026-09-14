using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Models;
using ResuceNet.ViewModels;

namespace ResuceNet.Controllers
{
    [Authorize(Roles = "Citizen")]
    public class CitizenController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CitizenController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            if (User.IsInRole("RescueTeam"))
            {
                return RedirectToAction("Dashboard", "RescueTeam");
            }
            if (!User.IsInRole("Citizen"))
            {
                return RedirectToAction("Index", "Home");
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int citizenId))
            {
                return RedirectToAction("Login", "Account");
            }

            var citizen = await _context.Users.FirstOrDefaultAsync(u => u.Id == citizenId);
            if (citizen == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Strictly filter by this logged-in citizen's ID
            var citizenRequests = await _context.EmergencyRequests
                .Where(r => r.CitizenId == citizenId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var requestIds = citizenRequests.Select(r => r.Id).ToList();

            // Fetch active assignments for these requests
            var assignments = await _context.EmergencyAssignments
                .Include(a => a.RescueTeam)
                .ThenInclude(t => t!.User)
                .Where(a => requestIds.Contains(a.EmergencyRequestId) && a.Status != "Rejected")
                .ToListAsync();

            // Fetch history logs for these requests
            var histories = await _context.EmergencyStatusHistories
                .Where(h => requestIds.Contains(h.EmergencyRequestId))
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            var activeList = new List<CitizenRequestItem>();
            var completedList = new List<CitizenRequestItem>();

            foreach (var req in citizenRequests)
            {
                var item = new CitizenRequestItem
                {
                    Request = req,
                    Assignment = assignments.FirstOrDefault(a => a.EmergencyRequestId == req.Id),
                    Histories = histories.Where(h => h.EmergencyRequestId == req.Id).ToList()
                };

                if (req.Status == "Resolved" || req.Status == "Completed")
                {
                    completedList.Add(item);
                }
                else
                {
                    activeList.Add(item);
                }
            }

            var viewModel = new CitizenDashboardViewModel
            {
                Citizen = citizen,
                TotalRequests = citizenRequests.Count,
                ActiveRequestsCount = activeList.Count,
                InProgressRequestsCount = activeList.Count(a => a.Request.Status == "On The Way" || a.Request.Status == "Rescue In Progress" || a.Request.Status == "Rescue Team Accepted"),
                ResolvedRequestsCount = completedList.Count,
                ActiveRequests = activeList,
                CompletedRequests = completedList
            };

            return View(viewModel);
        }
    }
}
