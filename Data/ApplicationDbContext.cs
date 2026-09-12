using Microsoft.EntityFrameworkCore;
using ResuceNet.Models;

namespace ResuceNet.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RescueTeam> RescueTeams { get; set; }
        public DbSet<EmergencyRequest> EmergencyRequests { get; set; }
        public DbSet<EmergencyAssignment> EmergencyAssignments { get; set; }
        public DbSet<EmergencyStatusHistory> EmergencyStatusHistories { get; set; }
        public DbSet<SMSAlert> SMSAlerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map entities to exact SQL table names
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<RescueTeam>().ToTable("RescueTeams");
            modelBuilder.Entity<EmergencyRequest>().ToTable("EmergencyRequests");
            modelBuilder.Entity<EmergencyAssignment>().ToTable("EmergencyAssignments");
            modelBuilder.Entity<EmergencyStatusHistory>().ToTable("EmergencyStatusHistory");
            modelBuilder.Entity<SMSAlert>().ToTable("SMSAlerts");

            // Configure decimal precision for Latitudes and Longitudes
            modelBuilder.Entity<RescueTeam>()
                .Property(t => t.CurrentLatitude)
                .HasPrecision(18, 10);

            modelBuilder.Entity<RescueTeam>()
                .Property(t => t.CurrentLongitude)
                .HasPrecision(18, 10);

            modelBuilder.Entity<EmergencyRequest>()
                .Property(e => e.Latitude)
                .HasPrecision(18, 10);

            modelBuilder.Entity<EmergencyRequest>()
                .Property(e => e.Longitude)
                .HasPrecision(18, 10);

            // Prevent cascade on delete for assignments -> rescue team to avoid multiple cascade path issues
            modelBuilder.Entity<EmergencyAssignment>()
                .HasOne(ea => ea.RescueTeam)
                .WithMany()
                .HasForeignKey(ea => ea.RescueTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmergencyAssignment>()
                .HasOne(ea => ea.EmergencyRequest)
                .WithMany()
                .HasForeignKey(ea => ea.EmergencyRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmergencyStatusHistory>()
                .HasOne(esh => esh.ChangedByUser)
                .WithMany()
                .HasForeignKey(esh => esh.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
