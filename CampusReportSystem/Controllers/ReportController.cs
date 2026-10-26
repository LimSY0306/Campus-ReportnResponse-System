using CampusReportSystem.Data;
using CampusReportSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CampusReportSystem.Controllers
{
    public class ReportController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            var actionName = filterContext.ActionDescriptor.RouteValues["action"];

            if (actionName != "ToggleLike" && HttpContext.Session.GetInt32("UserId") == null)
            {
                filterContext.Result = RedirectToAction("Login", "Account");
            }
        }

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReportController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            var currentUserId = HttpContext.Session.GetInt32("UserId");

            var query = _context.Reports
                .Include(r => r.Category)
                .Where(r => r.Category != null && r.Category.Name != "Announcement")
                .AsQueryable();

            if (userRole != "Admin")
            {
                query = query.Where(r => r.UserId == currentUserId);
            }

            var reports = await query
                .OrderByDescending(r => r.CreatedAT)
                .ToListAsync();

            return View(reports);
        }

        public IActionResult Create()
        {
            var categories = _context.Categories.Where(c => c.Name != "Announcement");
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Report report)
        {
            report.UserId = HttpContext.Session.GetInt32("UserId");

            if (ModelState.IsValid)
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    report.UserId = userId.Value;
                }

                if (report.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + report.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await report.ImageFile.CopyToAsync(fileStream);
                    }

                    report.ImagePath = "/images/" + uniqueFileName;
                }

                report.CreatedAT = DateTime.Now;
                _context.Add(report);
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Report", new { status = "created" });
            }

            var categories = _context.Categories.Where(c => c.Name != "Announcement");
            ViewBag.Categories = new SelectList(categories, "Id", "Name", report.CategoryId);
            return View(report);
        }

        // GET: Report/Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();

            var userRole = HttpContext.Session.GetString("UserRole");
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (userRole != "Admin" && report.UserId != currentUserId)
            {
                return RedirectToAction(nameof(Index));
            }

            var categories = _context.Categories.Where(c => c.Name != "Announcement");
            ViewBag.Categories = new SelectList(categories, "Id", "Name", report.CategoryId);
            return View(report);
        }

        // POST: Report/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Report report)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingReport = await _context.Reports.FindAsync(id);
                    if (existingReport == null) return NotFound();

                    var userRole = HttpContext.Session.GetString("UserRole");
                    var currentUserId = HttpContext.Session.GetInt32("UserId");
                    if (userRole != "Admin" && existingReport.UserId != currentUserId)
                    {
                        return RedirectToAction(nameof(Index));
                    }

                    existingReport.Title = report.Title;
                    existingReport.Description = report.Description;
                    existingReport.CategoryId = report.CategoryId;
                    existingReport.Latitude = report.Latitude;
                    existingReport.Longitude = report.Longitude;

                    if (report.ImageFile != null)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + report.ImageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await report.ImageFile.CopyToAsync(fileStream);
                        }

                        if (!string.IsNullOrEmpty(existingReport.ImagePath))
                        {
                            string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingReport.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        existingReport.ImagePath = "/images/" + uniqueFileName;
                    }

                    _context.Update(existingReport);
                    await _context.SaveChangesAsync();

                    return RedirectToAction("Index", "Report", new { status = "updated" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Reports.Any(e => e.Id == report.Id)) return NotFound();
                    else throw;
                }
            }

            var categories = _context.Categories.Where(c => c.Name != "Announcement");
            ViewBag.Categories = new SelectList(categories, "Id", "Name", report.CategoryId);
            return View(report);
        }

        // POST: Report/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report != null)
            {
                if (!string.IsNullOrEmpty(report.ImagePath))
                {
                    var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, report.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }

                _context.Reports.Remove(report);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Report", new { status = "deleted" });
        }

        // GET: Report/Detail
        public async Task<IActionResult> Detail(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.Category)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (report == null) return NotFound();

            return View(report);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleLike(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                if (userId == null || userId.Value == 0)
                {
                    return Json(new { success = false, requireLogin = true });
                }

                if (_context.Reactions == null)
                {
                    return Json(new { success = false, message = "Reactions table missing in DbContext" });
                }

                var existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => r.ReportId == id && r.UserId == userId.Value);

                bool isLiked;

                if (existingReaction != null)
                {
                    _context.Reactions.Remove(existingReaction);
                    isLiked = false;
                }
                else
                {
                    var reaction = new Reaction
                    {
                        ReportId = id,
                        UserId = userId.Value,
                        CreatedAt = DateTime.Now
                    };
                    _context.Reactions.Add(reaction);
                    isLiked = true;
                }

                await _context.SaveChangesAsync();

                int likeCount = await _context.Reactions.CountAsync(r => r.ReportId == id);

                return Json(new { success = true, isLiked = isLiked, likeCount = likeCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report != null)
            {
                report.Status = status;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { status = "updated" });
        }
    }
}