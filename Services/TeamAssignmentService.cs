using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ResuceNet.Data;
using ResuceNet.Models;

namespace ResuceNet.Services
{
    public class TeamAssignmentService : ITeamAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public TeamAssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RescueTeam?> FindNearestAvailableTeamAsync(decimal latitude, decimal longitude)
        {
            var availableTeams = await _context.RescueTeams
                .Where(t => t.Status == "Available" && t.CurrentLatitude.HasValue && t.CurrentLongitude.HasValue)
                .ToListAsync();

            RescueTeam? nearestTeam = null;
            double minDistance = double.MaxValue;

            foreach (var team in availableTeams)
            {
                double dist = CalculateDistance(
                    latitude, 
                    longitude, 
                    team.CurrentLatitude!.Value, 
                    team.CurrentLongitude!.Value
                );

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestTeam = team;
                }
            }

            return nearestTeam;
        }

        public double CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            // Haversine formula
            double dLat = ToRadians((double)(lat2 - lat1));
            double dLon = ToRadians((double)(lon2 - lon1));

            double rLat1 = ToRadians((double)lat1);
            double rLat2 = ToRadians((double)lat2);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(rLat1) * Math.Cos(rLat2);
            
            double c = 2 * Math.Asin(Math.Sqrt(a));
            
            // Radius of earth in kilometers
            const double R = 6371.0; 
            return R * c;
        }

        private static double ToRadians(double val)
        {
            return (Math.PI / 180.0) * val;
        }
    }
}
