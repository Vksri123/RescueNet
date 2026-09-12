using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ResuceNet.ViewModels
{
    public class CreateEmergencyViewModel
    {
        [Required(ErrorMessage = "Please select an emergency type.")]
        [Display(Name = "Emergency Type")]
        public string EmergencyType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please describe the emergency details.")]
        [MinLength(10, ErrorMessage = "Please provide a more detailed description (at least 10 characters).")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Latitude is required. Please select a location on the map or enable GPS.")]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
        public decimal Latitude { get; set; }

        [Required(ErrorMessage = "Longitude is required. Please select a location on the map or enable GPS.")]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
        public decimal Longitude { get; set; }

        [Display(Name = "Upload Image or Video (Optional)")]
        public IFormFile? MediaFile { get; set; }
    }
}
