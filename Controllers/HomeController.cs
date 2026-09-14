using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Models;

namespace ResuceNet.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentSMSAlerts()
        {
            try
            {
                var alerts = await _context.SMSAlerts
                    .OrderByDescending(s => s.SentAt)
                    .Take(20)
                    .Select(s => new
                    {
                        id = s.Id,
                        message = s.Message,
                        targetRole = s.TargetRole,
                        sentAt = s.SentAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt"),
                        timeAgo = s.SentAt.ToLocalTime().ToString("t"),
                        recipients = s.RecipientCount
                    })
                    .ToListAsync();

                int currentUserId = 0;
                int userLastSeenId = 0;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out currentUserId) && currentUserId > 0)
                {
                    try
                    {
                        var state = await _context.UserNotificationStates.FirstOrDefaultAsync(u => u.UserId == currentUserId);
                        if (state != null)
                        {
                            userLastSeenId = state.LastSeenSMSAlertId;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not query UserNotificationStates: {Message}", ex.Message);
                    }
                }

                return Json(new { success = true, alerts = alerts, count = alerts.Count, userLastSeenId = userLastSeenId, userId = currentUserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SMS alerts");
                return Json(new { success = false, alerts = new object[0], count = 0, userLastSeenId = 0, userId = 0 });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkSMSAlertsAsSeen([FromBody] MarkSeenRequest request)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int currentUserId) || currentUserId <= 0)
            {
                return BadRequest();
            }

            int lastSeenId = request?.LastSeenId ?? 0;
            if (lastSeenId <= 0)
            {
                return Json(new { success = false });
            }

            try
            {
                var state = await _context.UserNotificationStates.FirstOrDefaultAsync(u => u.UserId == currentUserId);
                if (state == null)
                {
                    state = new UserNotificationState
                    {
                        UserId = currentUserId,
                        LastSeenSMSAlertId = lastSeenId,
                        LastSeenAt = DateTime.UtcNow
                    };
                    _context.UserNotificationStates.Add(state);
                }
                else
                {
                    if (lastSeenId > state.LastSeenSMSAlertId)
                    {
                        state.LastSeenSMSAlertId = lastSeenId;
                        state.LastSeenAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, lastSeenId = state.LastSeenSMSAlertId, userId = currentUserId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking SMS alerts seen for user {UserId}", currentUserId);
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                if (role == "RescueTeam")
                {
                    return RedirectToAction("Dashboard", "RescueTeam");
                }
                return RedirectToAction("Create", "Emergency");
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class MarkSeenRequest
    {
        public int LastSeenId { get; set; }
    }
}
