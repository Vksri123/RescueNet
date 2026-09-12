using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    public class EmergencyAssignment
    {
        public int Id { get; set; }

        [Required]
        public int EmergencyRequestId { get; set; }

        [ForeignKey("EmergencyRequestId")]
        public virtual EmergencyRequest? EmergencyRequest { get; set; }

        [Required]
        public int RescueTeamId { get; set; }

        [ForeignKey("RescueTeamId")]
        public virtual RescueTeam? RescueTeam { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Assigned"; // 'Assigned', 'Accepted', 'OnTheWay', 'InProgress', 'Completed', 'Rejected'
    }
}
