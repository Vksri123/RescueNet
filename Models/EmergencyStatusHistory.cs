using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    [Table("EmergencyStatusHistory")]
    public class EmergencyStatusHistory
    {
        public int Id { get; set; }

        [Required]
        public int EmergencyRequestId { get; set; }

        [ForeignKey("EmergencyRequestId")]
        public virtual EmergencyRequest? EmergencyRequest { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public int? ChangedByUserId { get; set; }

        [ForeignKey("ChangedByUserId")]
        public virtual User? ChangedByUser { get; set; }
    }
}
