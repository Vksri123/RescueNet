using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    [Table("SMSAlerts")]
    public class SMSAlert
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TargetRole { get; set; } = "All"; // 'All', 'Citizen', 'RescueTeam'

        public int RecipientCount { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public int? SentByUserId { get; set; }

        [ForeignKey("SentByUserId")]
        public virtual User? SentByUser { get; set; }
    }
}
