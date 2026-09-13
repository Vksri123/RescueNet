using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    public class EmergencyRequest
    {
        public int Id { get; set; }

        [Required]
        public int CitizenId { get; set; }

        [ForeignKey("CitizenId")]
        public virtual User? Citizen { get; set; }

        [Required]
        [StringLength(100)]
        public string EmergencyType { get; set; } = string.Empty; // e.g. 'Flood', 'Fire', 'Accident', etc.

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18, 10)")]
        public decimal Latitude { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 10)")]
        public decimal Longitude { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Created"; // 'Created', 'Assigned', 'Rescue Team Accepted', 'On The Way', 'Rescue In Progress', 'Resolved'

        [Required]
        [StringLength(50)]
        public string PriorityLevel { get; set; } = "Medium"; // 'Low', 'Medium', 'High', 'Critical'

        public int RiskScore { get; set; } = 0; // Calculated 0-100

        public string? MediaUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
