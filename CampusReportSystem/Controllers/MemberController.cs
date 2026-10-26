using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;
using CampusReportSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CampusReportSystem.Controllers
{
    public class MemberController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MemberController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Ensure only Admin role can access this controller
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
            {
                filterContext.Result = RedirectToAction("Login", "Account");
            }
        }

        // GET: Member
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortBy = "CreatedAt", string sortOrder = "desc", int page = 1, bool ajax = false)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["SortBy"] = sortBy;
            ViewData["SortOrder"] = sortOrder;

            int pageSize = 5;

            var members = _context.Users
                .Include(u => u.Reports)
                .Where(u => u.Role == "User" || u.Role == "Member");

            if (!string.IsNullOrEmpty(searchString))
                members = members.Where(u => u.Name.Contains(searchString) || u.Email.Contains(searchString));

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "active")
                    members = members.Where(u => u.IsActive);
                else if (statusFilter == "inactive")
                    members = members.Where(u => !u.IsActive);
            }

            // Apply sorting
            members = sortBy.ToLower() switch
            {
                "formattedid" => sortOrder == "asc" ? members.OrderBy(u => u.RoleSeqNo) : members.OrderByDescending(u => u.RoleSeqNo),
                "name" => sortOrder == "asc" ? members.OrderBy(u => u.Name) : members.OrderByDescending(u => u.Name),
                "email" => sortOrder == "asc" ? members.OrderBy(u => u.Email) : members.OrderByDescending(u => u.Email),
                "isactive" => sortOrder == "asc" ? members.OrderBy(u => u.IsActive) : members.OrderByDescending(u => u.IsActive),
                "reportscount" => sortOrder == "asc" ? members.OrderBy(u => u.Reports.Count) : members.OrderByDescending(u => u.Reports.Count),
                _ => sortOrder == "asc" ? members.OrderBy(u => u.CreatedAt) : members.OrderByDescending(u => u.CreatedAt)
            };

            int totalCount = await members.CountAsync();

            var paginatedMembers = await members
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["TotalPages"] = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;

            if (ajax)
                return PartialView("_TableRows", paginatedMembers);

            return View(paginatedMembers);
        }

        // GET: Member/Profile
        public async Task<IActionResult> Profile()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();

            return View(user);
        }

        // POST: Member/UploadProfilePicture
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture([FromBody] ImageUploadModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            if (string.IsNullOrEmpty(model?.Base64Image))
                return Json(new { success = false, message = "Invalid image data." });

            try
            {
                var base64Data = model.Base64Image.Contains(",") ? model.Base64Image.Split(',')[1] : model.Base64Image;
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = $"profile_{userId.Value}_{DateTime.Now.Ticks}.png";
                string filePath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                var user = await _context.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, imagePath = user?.ProfilePicturePath, message = "Profile picture updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Member/ReportHistory
        public async Task<IActionResult> ReportHistory(int? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Users
                .Include(u => u.Reports)
                    .ThenInclude(r => r.Category)
                .FirstOrDefaultAsync(m => m.Id == id && (m.Role == "User" || m.Role == "Member"));

            if (member == null) return NotFound();

            return View(member);
        }

        // GET: Member/Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Users
                .Include(u => u.Reports)
                    .ThenInclude(r => r.Category)
                .FirstOrDefaultAsync(m => m.Id == id && (m.Role == "User" || m.Role == "Member"));

            if (member == null) return NotFound();

            return View(member);
        }

        // GET: Member/Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Users.FindAsync(id);
            if (member == null || (member.Role != "User" && member.Role != "Member")) return NotFound();

            var model = new EditUserViewModel
            {
                Id = member.Id,
                Name = member.Name,
                Email = member.Email,
                IsActive = member.IsActive,
                Role = member.Role,
                CreatedAt = member.CreatedAt
            };
            return View(model);
        }

        // POST: Member/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var member = await _context.Users.FindAsync(id);
            if (member == null || (member.Role != "User" && member.Role != "Member")) return NotFound();

            if (_context.Users.Any(u => u.Email == model.Email && u.Id != model.Id))
                ModelState.AddModelError("Email", "This email address is already used by another account.");

            if (!string.IsNullOrWhiteSpace(model.Password) && model.Password.Length < 6)
                ModelState.AddModelError("Password", "Password must be at least 6 characters.");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            member.Name = model.Name;
            member.Email = model.Email;
            member.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var hasher = new PasswordHasher<User>();
                member.Password = hasher.HashPassword(member, model.Password);
            }

            try
            {
                _context.Update(member);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Member account updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.Id == member.Id && (e.Role == "User" || e.Role == "Member")))
                    return NotFound();
                else
                    throw;
            }
        }

        // POST: Member/ToggleStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var member = await _context.Users.FindAsync(id);
            if (member == null || (member.Role != "User" && member.Role != "Member")) return NotFound();

            member.IsActive = !member.IsActive;
            _context.Update(member);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Member account status updated to {(member.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Member/Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Users.FirstOrDefaultAsync(m => m.Id == id && (m.Role == "User" || m.Role == "Member"));
            if (member == null) return NotFound();

            return View(member);
        }

        // POST: Member/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var member = await _context.Users.FindAsync(id);
            if (member != null && (member.Role == "User" || member.Role == "Member"))
            {
                _context.Users.Remove(member);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Member account deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        public class ImageUploadModel
        {
            public string Base64Image { get; set; } = string.Empty;
        }
    }
}