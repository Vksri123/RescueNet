using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResuceNet.Models
{
    [Table("UserNotificationStates")]
    public class UserNotificationState
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public int LastSeenSMSAlertId { get; set; }

        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
