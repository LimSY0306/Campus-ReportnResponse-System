using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;
using CampusReportSystem.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;

namespace CampusReportSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
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

        // GET: Admin
        public async Task<IActionResult> Index(string searchString, string statusFilter, string sortBy = "CreatedAt", string sortOrder = "desc", int page = 1, bool ajax = false)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["SortBy"] = sortBy;
            ViewData["SortOrder"] = sortOrder;

            int pageSize = 5;

            var admins = _context.Users
                .Include(u => u.Reports)
                .Where(u => u.Role == "Admin");

            if (!string.IsNullOrEmpty(searchString))
            {
                admins = admins.Where(u => u.Name.Contains(searchString) || u.Email.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                if (statusFilter == "active")
                    admins = admins.Where(u => u.IsActive);
                else if (statusFilter == "inactive")
                    admins = admins.Where(u => !u.IsActive);
            }

            // Apply sorting
            admins = sortBy.ToLower() switch
            {
                "formattedid" => sortOrder == "asc" ? admins.OrderBy(u => u.RoleSeqNo) : admins.OrderByDescending(u => u.RoleSeqNo),
                "name" => sortOrder == "asc" ? admins.OrderBy(u => u.Name) : admins.OrderByDescending(u => u.Name),
                "email" => sortOrder == "asc" ? admins.OrderBy(u => u.Email) : admins.OrderByDescending(u => u.Email),
                "isactive" => sortOrder == "asc" ? admins.OrderBy(u => u.IsActive) : admins.OrderByDescending(u => u.IsActive),
                "reportscount" => sortOrder == "asc" ? admins.OrderBy(u => u.Reports.Count) : admins.OrderByDescending(u => u.Reports.Count),
                _ => sortOrder == "asc" ? admins.OrderBy(u => u.CreatedAt) : admins.OrderByDescending(u => u.CreatedAt)
            };

            int totalCount = await admins.CountAsync();

            var paginatedAdmins = await admins
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["TotalPages"] = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;

            if (ajax)
                return PartialView("_TableRows", paginatedAdmins);

            return View(paginatedAdmins);
        }

        // GET: Admin/ReportHistory
        public async Task<IActionResult> ReportHistory(int? id)
        {
            if (id == null) return NotFound();

            var admin = await _context.Users
                .Include(u => u.Reports)
                    .ThenInclude(r => r.Category)
                .FirstOrDefaultAsync(m => m.Id == id && m.Role == "Admin");

            if (admin == null) return NotFound();

            return View(admin);
        }

        // GET: Admin/Assign
        public async Task<IActionResult> Assign()
        {
            var members = await _context.Users
                .Where(u => u.Role == "User" || u.Role == "Member")
                .OrderBy(u => u.Name)
                .ToListAsync();

            return View(members);
        }

        // POST: Admin/Assign – promote selected members to Admin role
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["ErrorMessage"] = "No members selected.";
                return RedirectToAction(nameof(Assign));
            }

            var members = await _context.Users
                .Where(u => (u.Role == "User" || u.Role == "Member") && selectedIds.Contains(u.Id))
                .ToListAsync();

            if (!members.Any())
            {
                TempData["ErrorMessage"] = "Selected members not found.";
                return RedirectToAction(nameof(Assign));
            }

            // Get current max RoleSeqNo for Admin to assign new sequential numbers
            int maxAdminSeq = await _context.Users
                .Where(u => u.Role == "Admin")
                .MaxAsync(u => (int?)u.RoleSeqNo) ?? 0;

            foreach (var user in members)
            {
                user.Role = "Admin";
                user.RoleSeqNo = ++maxAdminSeq;
                _context.Update(user);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{members.Count} member(s) promoted to Admin successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Profile
        public async Task<IActionResult> Profile()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value && u.Role == "Admin");
            if (admin == null)
                return NotFound();

            return View(admin);
        }

        // POST: Admin/UploadProfilePicture
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

                string fileName = $"profile_admin_{userId.Value}_{DateTime.Now.Ticks}.png";
                string filePath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value && u.Role == "Admin");
                if (admin != null)
                {
                    admin.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                    _context.Update(admin);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, imagePath = admin?.ProfilePicturePath, message = "Profile picture updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Admin/Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var admin = await _context.Users.FirstOrDefaultAsync(m => m.Id == id && m.Role == "Admin");
            if (admin == null) return NotFound();

            return View(admin);
        }

        // GET: Admin/Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var admin = await _context.Users.FindAsync(id);
            if (admin == null || admin.Role != "Admin") return NotFound();

            var model = new EditUserViewModel
            {
                Id = admin.Id,
                Name = admin.Name,
                Email = admin.Email,
                IsActive = admin.IsActive,
                Role = admin.Role,
                CreatedAt = admin.CreatedAt
            };
            return View(model);
        }

        // POST: Admin/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditUserViewModel model)
        {
            if (id != model.Id) return NotFound();

            var admin = await _context.Users.FindAsync(id);
            if (admin == null || admin.Role != "Admin") return NotFound();

            if (_context.Users.Any(u => u.Email == model.Email && u.Id != model.Id))
                ModelState.AddModelError("Email", "This email address is already used by another account.");

            if (!string.IsNullOrWhiteSpace(model.Password) && model.Password.Length < 6)
                ModelState.AddModelError("Password", "Password must be at least 6 characters.");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            admin.Name = model.Name;
            admin.Email = model.Email;
            admin.IsActive = model.IsActive;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var hasher = new PasswordHasher<User>();
                admin.Password = hasher.HashPassword(admin, model.Password);
            }

            try
            {
                _context.Update(admin);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Admin account updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.Id == admin.Id && e.Role == "Admin"))
                    return NotFound();
                else
                    throw;
            }
        }

        // POST: Admin/ToggleStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var admin = await _context.Users.FindAsync(id);
            if (admin == null || admin.Role != "Admin") return NotFound();

            admin.IsActive = !admin.IsActive;
            _context.Update(admin);
            await _context.SaveChangesAsync();

            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId.HasValue && currentUserId.Value == id && !admin.IsActive)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            TempData["SuccessMessage"] = $"Admin account status updated to {(admin.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var admin = await _context.Users.FirstOrDefaultAsync(m => m.Id == id && m.Role == "Admin");
            if (admin == null) return NotFound();

            return View(admin);
        }

        // POST: Admin/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var admin = await _context.Users.FindAsync(id);
            if (admin != null && admin.Role == "Admin")
            {
                _context.Users.Remove(admin);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Admin account deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        public class ImageUploadModel
        {
            public string Base64Image { get; set; } = string.Empty;
        }
    }
}
