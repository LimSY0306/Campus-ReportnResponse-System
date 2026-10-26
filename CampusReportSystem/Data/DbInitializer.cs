using CampusReportSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace CampusReportSystem.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            var hasher = new PasswordHasher<User>();

            // Create default admin ONLY if no admin exists at all
            if (!context.Users.Any(u => u.Role == "Admin"))
            {
                var admin = new User
                {
                    RoleSeqNo = 1,
                    Name = "Admin User",
                    Email = "admin@gmail.com",
                    PhoneNumber = "0123456789",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                admin.Password = hasher.HashPassword(admin, "Admin123");
                context.Users.Add(admin);
            }

            // Create default member ONLY if no member/user exists
            if (!context.Users.Any(u => u.Role == "User" || u.Role == "Member"))
            {
                var member = new User
                {
                    RoleSeqNo = 1,
                    Name = "Sample Member",
                    Email = "member@gmail.com",
                    PhoneNumber = "0123456789",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                member.Password = hasher.HashPassword(member, "Member123");
                context.Users.Add(member);
            }

            context.SaveChanges();
        }
    }
}