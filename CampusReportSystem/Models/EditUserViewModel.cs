using System.ComponentModel.DataAnnotations;

namespace CampusReportSystem.Models
{
    public class EditUserViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Password is optional for editing
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string? Password { get; set; }

        public bool IsActive { get; set; }

        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}