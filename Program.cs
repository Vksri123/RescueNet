using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Hubs;
using ResuceNet.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register ApplicationDbContext:
// Uses local SQL Server on Windows PC, and auto-switches to portable SQLite in GitHub Codespaces / cloud
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
bool isCodespaces = Environment.GetEnvironmentVariable("CODESPACES") == "true";
bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
string? dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider");

bool useSqlite = isCodespaces 
                 || !isWindows 
                 || dbProvider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true
                 || string.IsNullOrWhiteSpace(connectionString)
                 || connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);

if (useSqlite)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=rescuenet.db"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// Register SignalR
builder.Services.AddSignalR();

// Register HttpClient & Custom Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPriorityService, PriorityService>();
builder.Services.AddScoped<ITeamAssignmentService, TeamAssignmentService>();
builder.Services.AddScoped<IEmergencyService, EmergencyService>();
builder.Services.AddSingleton<ISMSService, SMSService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication MUST be called before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<EmergencyHub>("/emergencyHub");

// Auto-initialize Database and seed default accounts (for Codespaces & new environments)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        // Seed default Admin if not present
        if (!db.Users.Any(u => u.Role == "Admin"))
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Admin@123"));
            var sb = new System.Text.StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("X2"));

            db.Users.Add(new ResuceNet.Models.User
            {
                Username = "admin",
                PasswordHash = sb.ToString(),
                FullName = "Command HQ Admin",
                PhoneNumber = "9999999999",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        // Seed default Rescue Team if none exists
        if (!db.RescueTeams.Any())
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Team@123"));
            var sb = new System.Text.StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("X2"));

            var teamUser = new ResuceNet.Models.User
            {
                Username = "rescueteam1",
                PasswordHash = sb.ToString(),
                FullName = "Alpha Rescue Squad",
                PhoneNumber = "9876543210",
                Role = "RescueTeam",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(teamUser);
            db.SaveChanges();

            db.RescueTeams.Add(new ResuceNet.Models.RescueTeam
            {
                UserId = teamUser.Id,
                TeamName = "Alpha Rescue Squad",
                ContactNumber = "9876543210",
                CurrentLatitude = 23.0225m,
                CurrentLongitude = 72.5714m,
                Status = "Available",
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Init Note]: {ex.Message}");
    }
}

app.Run();

