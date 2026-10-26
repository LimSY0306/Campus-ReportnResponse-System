using System.ComponentModel.DataAnnotations;

namespace CampusReportSystem.Models
{
    public class Reaction
    {
        [Key]
        public int Id { get; set; }

        public int ReportId { get; set; }
        public Report? Report { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}