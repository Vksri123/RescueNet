using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    public class RescueTeam
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        [StringLength(150)]
        public string TeamName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 10)")]
        public decimal? CurrentLatitude { get; set; }

        [Column(TypeName = "decimal(18, 10)")]
        public decimal? CurrentLongitude { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Available"; // 'Available', 'Busy', 'Offline'

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
