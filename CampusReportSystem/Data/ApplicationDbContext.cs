using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Models;

namespace CampusReportSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; } // <--- Update property name to Feedbacks
        public DbSet<SecurityLog> SecurityLogs { get; set; }
        public DbSet<Reaction> Reactions { get; set; }
    }
}