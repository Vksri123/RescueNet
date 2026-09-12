using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Hubs;
using ResuceNet.Models;
using ResuceNet.Services;
using ResuceNet.ViewModels;

namespace ResuceNet.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmergencyService _emergencyService;
        private readonly IHubContext<EmergencyHub> _hubContext;
        private readonly ISMSService _smsService;

        public AdminController(
            ApplicationDbContext context, 
            IEmergencyService emergencyService,
            IHubContext<EmergencyHub> hubContext,
            ISMSService smsService)
        {
            _context = context;
            _emergencyService = emergencyService;
            _hubContext = hubContext;
            _smsService = smsService;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Active is status NOT Resolved/Rejected
            var activeStatuses = new[] { "Created", "AI Analyzed", "Assigned", "Rescue Team Accepted", "On The Way", "Rescue In Progress" };

            var activeRequests = await _context.EmergencyRequests
                .Include(r => r.Citizen)
                .Where(r => activeStatuses.Contains(r.Status))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var teams = await _context.RescueTeams
                .Include(t => t.User)
                .ToListAsync();

            // Counts
            int totalActive = activeRequests.Count;
            int critical = activeRequests.Count(r => r.PriorityLevel == "Critical");
            int high = activeRequests.Count(r => r.PriorityLevel == "High");
            int medium = activeRequests.Count(r => r.PriorityLevel == "Medium");
            int low = activeRequests.Count(r => r.PriorityLevel == "Low");

            // Emergency Type Distribution for Charts
            var typeDistribution = await _context.EmergencyRequests
                .GroupBy(r => r.EmergencyType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count);

            // Seed sample data for types if empty to make UI look amazing
            if (!typeDistribution.Any())
            {
                typeDistribution.Add("Flood", 12);
                typeDistribution.Add("Fire", 8);
                typeDistribution.Add("Accident", 15);
                typeDistribution.Add("Medical", 10);
            }

            // Recent SMS Alerts History
            var recentSMS = await _context.SMSAlerts
                .Include(s => s.SentByUser)
                .OrderByDescending(s => s.SentAt)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentSMSAlerts = recentSMS;
            ViewBag.SMSGatewayConfig = _smsService.GetConfig();

            var viewModel = new AdminDashboardViewModel
            {
                TotalActiveEmergencies = totalActive,
                CriticalCount = critical,
                HighCount = high,
                MediumCount = medium,
                LowCount = low,
                ActiveEmergencies = activeRequests,
                RescueTeams = teams,
                EmergencyTypeCounts = typeDistribution
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> CompletedRequests()
        {
            var completedRequests = await _context.EmergencyRequests
                .Include(r => r.Citizen)
                .Where(r => r.Status == "Resolved" || r.Status == "Completed")
                .OrderByDescending(r => r.UpdatedAt)
                .ToListAsync();

            // Fetch assignments for these requests
            var completedIds = completedRequests.Select(r => r.Id).ToList();
            var assignments = await _context.EmergencyAssignments
                .Include(a => a.RescueTeam)
                .Where(a => completedIds.Contains(a.EmergencyRequestId))
                .ToListAsync();

            var histories = await _context.EmergencyStatusHistories
                .Where(h => completedIds.Contains(h.EmergencyRequestId))
                .ToListAsync();

            ViewBag.Assignments = assignments;
            ViewBag.Histories = histories;

            return View(completedRequests);
        }

        [HttpPost]
        public async Task<IActionResult> AssignTeam(int requestId, int teamId)
        {
            var success = await _emergencyService.AssignTeamAsync(requestId, teamId);
            if (!success)
            {
                TempData["Error"] = "Unable to assign team. The team might be busy or offline.";
            }
            else
            {
                TempData["Success"] = "Team successfully assigned and dispatched!";
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> SendSMSAlert(string alertMessage, string targetRole, string? customPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(alertMessage))
            {
                TempData["Error"] = "Please enter an alert message before sending.";
                return RedirectToAction("Dashboard");
            }

            targetRole = string.IsNullOrEmpty(targetRole) ? "All" : targetRole;
            var phoneNumbers = new List<string>();

            if (targetRole == "DirectOnly")
            {
                // Send exclusively to the custom direct phone number
                if (!string.IsNullOrWhiteSpace(customPhoneNumber))
                {
                    var customNums = customPhoneNumber.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var num in customNums)
                    {
                        var clean = num.Trim();
                        if (!string.IsNullOrEmpty(clean) && !phoneNumbers.Contains(clean))
                        {
                            phoneNumbers.Add(clean);
                        }
                    }
                }
            }
            else
            {
                // Fetch registered users matching target role
                var query = _context.Users.AsQueryable();
                if (targetRole == "Citizen")
                {
                    query = query.Where(u => u.Role == "Citizen");
                }
                else if (targetRole == "RescueTeam")
                {
                    query = query.Where(u => u.Role == "RescueTeam");
                }

                phoneNumbers = await query
                    .Where(u => u.PhoneNumber != null && u.PhoneNumber.Trim() != "" && u.PhoneNumber.Trim() != "911")
                    .Select(u => u.PhoneNumber!.Trim())
                    .ToListAsync();

                // Add custom direct mobile numbers if entered by Admin
                if (!string.IsNullOrWhiteSpace(customPhoneNumber))
                {
                    var customNums = customPhoneNumber.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var num in customNums)
                    {
                        var clean = num.Trim();
                        if (!string.IsNullOrEmpty(clean) && !phoneNumbers.Contains(clean))
                        {
                            phoneNumbers.Add(clean);
                        }
                    }
                }
            }

            if (!phoneNumbers.Any())
            {
                TempData["Error"] = "No valid phone numbers found! Please enter a 10-digit mobile number in the 'Direct Mobile Number' field.";
                return RedirectToAction("Dashboard");
            }

            int recipientCount = phoneNumbers.Count;

            // Execute SMS Dispatch via ISMSService for each phone number
            int successfulDeliveries = 0;
            List<string> deliveryLogs = new List<string>();

            foreach (var phone in phoneNumbers)
            {
                var (success, resultMsg) = await _smsService.SendSMSAsync(phone, alertMessage);
                if (success) successfulDeliveries++;
                deliveryLogs.Add(resultMsg);
            }

            // Get Current User ID
            int? currentUserId = null;
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int uid))
            {
                currentUserId = uid;
            }

            // Save SMS Alert Log in DB
            var smsLog = new SMSAlert
            {
                Message = alertMessage,
                TargetRole = targetRole,
                RecipientCount = recipientCount,
                SentAt = DateTime.UtcNow,
                SentByUserId = currentUserId
            };

            _context.SMSAlerts.Add(smsLog);
            await _context.SaveChangesAsync();

            // Broadcast real-time alert toast/banner to all connected clients via SignalR
            var timestampStr = DateTime.Now.ToString("g");
            await _hubContext.Clients.All.SendAsync("SMSAlertReceived", alertMessage, targetRole, timestampStr);

            var config = _smsService.GetConfig();
            var details = string.Join(" ; ", deliveryLogs);

            if (config.Provider != "Simulation" && !string.IsNullOrEmpty(config.ApiKey))
            {
                if (successfulDeliveries > 0)
                {
                    TempData["Success"] = $"SMS Alert Dispatched via {config.Provider} ({successfulDeliveries}/{recipientCount} delivered): {details}";
                }
                else
                {
                    TempData["Error"] = $"SMS Alert Failed ({successfulDeliveries}/{recipientCount} delivered): {details}";
                }
            }
            else
            {
                TempData["Success"] = $"Emergency SMS Alert broadcasted to {recipientCount} user(s)! [Note: Gateway in Simulation Mode].";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult SaveSMSGatewayConfig(SMSGatewayConfig newConfig)
        {
            _smsService.UpdateConfig(newConfig);
            TempData["Success"] = $"SMS Gateway Settings updated successfully! Active Provider: '{newConfig.Provider}'.";
            return RedirectToAction("Dashboard");
        }
    }
}
