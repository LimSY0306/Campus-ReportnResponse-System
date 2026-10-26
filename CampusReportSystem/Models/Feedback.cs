using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusReportSystem.Models
{
    public class Feedback
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required(ErrorMessage = "Please select a feedback type.")]
        [StringLength(50)]
        public string Type { get; set; } = "General";

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(100)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required.")]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; } = 5;

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public string? AdminResponse { get; set; }

        // Added reaction counts
        public int LikesCount { get; set; } = 0;

        public int DislikesCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}