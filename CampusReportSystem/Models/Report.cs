using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace CampusReportSystem.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter the title.")]
        [Display(Name = "Report Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please description the problem.")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public DateTime CreatedAT { get; set; } = DateTime.Now;

        [NotMapped]
        [Display(Name = "Upload Photo")]
        public IFormFile? ImageFile { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string Status { get; set; } = "Pending";
    }
}