using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Models;
using ResuceNet.ViewModels;

namespace ResuceNet.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Username = model.Username?.Trim() ?? string.Empty;

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "Username is already taken.");
                return View(model);
            }

            // Rescue team requirements
            if (model.Role == "RescueTeam" && (string.IsNullOrEmpty(model.TeamName) || string.IsNullOrEmpty(model.TeamContactNumber)))
            {
                ModelState.AddModelError("", "Rescue Team details (Name and Contact Number) are required for Rescue Teams.");
                return View(model);
            }

            // Create new User
            var user = new User
            {
                Username = model.Username,
                PasswordHash = HashPassword(model.Password),
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                Role = model.Role,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create Rescue Team record if role is RescueTeam
            if (user.Role == "RescueTeam")
            {
                var team = new RescueTeam
                {
                    UserId = user.Id,
                    TeamName = model.TeamName ?? $"{model.FullName}'s Team",
                    ContactNumber = model.TeamContactNumber ?? model.PhoneNumber ?? "911",
                    CurrentLatitude = 23.0225m, // Default coordinates (Ahmedabad Center)
                    CurrentLongitude = 72.5714m,
                    Status = "Available",
                    CreatedAt = DateTime.UtcNow
                };
                _context.RescueTeams.Add(team);
                await _context.SaveChangesAsync();
            }

            // Sign In the user
            await SignInUserAsync(user);

            return RedirectToDashboard(user.Role);
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = model.Username?.Trim() ?? string.Empty;
            var hash = HashPassword(model.Password);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            await SignInUserAsync(user);

            return RedirectToDashboard(user.Role);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (var b in hashedBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        private IActionResult RedirectToDashboard(string? role = null)
        {
            role ??= User.FindFirst(ClaimTypes.Role)?.Value ?? 
                     HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            if (role == "RescueTeam")
            {
                return RedirectToAction("Dashboard", "RescueTeam");
            }
            if (role == "Citizen")
            {
                return RedirectToAction("Dashboard", "Citizen");
            }
            return RedirectToAction("Index", "Home");
        }
    }
}
