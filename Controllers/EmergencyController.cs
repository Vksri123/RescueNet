using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Services;
using ResuceNet.ViewModels;

namespace ResuceNet.Controllers
{
    [Authorize]
    public class EmergencyController : Controller
    {
        private readonly IEmergencyService _emergencyService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public EmergencyController(
            IEmergencyService emergencyService, 
            ApplicationDbContext context, 
            IWebHostEnvironment env)
        {
            _emergencyService = emergencyService;
            _context = context;
            _env = env;
        }

        [HttpGet]
        public IActionResult Create()
        {
            // Set Ahmedabad center as default coordinates
            var model = new CreateEmergencyViewModel
            {
                Latitude = 23.0225m,
                Longitude = 72.5714m
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEmergencyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int citizenId))
            {
                return RedirectToAction("Login", "Account");
            }

            string? mediaUrl = null;

            // Handle file upload
            if (model.MediaFile != null && model.MediaFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.MediaFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.MediaFile.CopyToAsync(fileStream);
                }

                mediaUrl = "/uploads/" + uniqueFileName;
            }

            var request = await _emergencyService.CreateEmergencyAsync(
                citizenId,
                model.EmergencyType,
                model.Description,
                model.Latitude,
                model.Longitude,
                mediaUrl
            );

            return RedirectToAction("Track", new { id = request.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Track(int id)
        {
            var request = await _context.EmergencyRequests
                .Include(r => r.Citizen)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            // Security: If logged-in user is a Citizen, verify request ownership
            if (User.IsInRole("Citizen"))
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdString, out int currentCitizenId) && request.CitizenId != currentCitizenId)
                {
                    return Forbid();
                }
            }

            // Fetch history
            var history = await _context.EmergencyStatusHistories
                .Where(h => h.EmergencyRequestId == id)
                .OrderBy(h => h.ChangedAt)
                .ToListAsync();

            // Fetch assignments & assigned team
            var assignment = await _context.EmergencyAssignments
                .Include(a => a.RescueTeam)
                .ThenInclude(t => t!.User)
                .FirstOrDefaultAsync(a => a.EmergencyRequestId == id && a.Status != "Rejected");

            ViewBag.History = history;
            ViewBag.Assignment = assignment;

            return View(request);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.EmergencyRequests
                .Include(r => r.Citizen)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            // Security: If logged-in user is a Citizen, verify request ownership
            if (User.IsInRole("Citizen"))
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdString, out int currentCitizenId) && request.CitizenId != currentCitizenId)
                {
                    return Forbid();
                }
            }

            var history = await _context.EmergencyStatusHistories
                .Where(h => h.EmergencyRequestId == id)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            var assignment = await _context.EmergencyAssignments
                .Include(a => a.RescueTeam)
                .FirstOrDefaultAsync(a => a.EmergencyRequestId == id && a.Status != "Rejected");

            ViewBag.History = history;
            ViewBag.Assignment = assignment;

            return View(request);
        }
    }
}
