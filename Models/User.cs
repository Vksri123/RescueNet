using System;
using System.ComponentModel.DataAnnotations;

namespace ResuceNet.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = "Citizen"; // 'Citizen', 'RescueTeam', 'Admin'

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
