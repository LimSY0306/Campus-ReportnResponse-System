using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;
using CampusReportSystem.Models;

namespace CampusReportSystem.Controllers
{
    public class AnnouncementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var reportsQuery = _context.Reports
                .Include(r => r.Category)
                .Where(r => r.Category != null && r.Category.Name == "Announcement");

            if (!string.IsNullOrEmpty(searchString))
            {
                reportsQuery = reportsQuery.Where(r => 
                    (r.Title != null && r.Title.Contains(searchString)) || 
                    (r.Description != null && r.Description.Contains(searchString)));
            }

            var announcements = await reportsQuery
                .OrderByDescending(r => r.CreatedAT)
                .ToListAsync();

            return View(announcements);
        }

        public async Task<IActionResult> Manage(string searchString)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            ViewData["CurrentFilter"] = searchString;

            var reportsQuery = _context.Reports
                .Include(r => r.Category)
                .Where(r => r.Category != null && r.Category.Name == "Announcement");

            if (!string.IsNullOrEmpty(searchString))
            {
                reportsQuery = reportsQuery.Where(r => 
                    (r.Title != null && r.Title.Contains(searchString)) || 
                    (r.Description != null && r.Description.Contains(searchString)));
            }

            var announcements = await reportsQuery
                .OrderByDescending(r => r.CreatedAT)
                .ToListAsync();

            return View(announcements);
        }

        // GET: /Announcement/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var announcement = await _context.Reports
                .Include(r => r.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (announcement == null)
            {
                return NotFound();
            }

            return View(announcement);
        }

        // GET: /Announcement/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        // POST: /Announcement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string description)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError("", "Title and Description are required.");
                return View();
            }

            var announcementCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Announcement");

            if (announcementCategory == null)
            {
                announcementCategory = new Category { Name = "Announcement" };
                _context.Categories.Add(announcementCategory);
                await _context.SaveChangesAsync();
            }

            var newAnnouncement = new Report
            {
                Title = title,
                Description = description,
                CreatedAT = DateTime.Now,
                CategoryId = announcementCategory.Id
            };

            _context.Reports.Add(newAnnouncement);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        // GET: /Announcement/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            if (id == null)
            {
                return NotFound();
            }

            var announcement = await _context.Reports.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            return View(announcement);
        }

        // POST: /Announcement/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string title, string description)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            var announcement = await _context.Reports.FindAsync(id);
            if (announcement == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError("", "Title and Description are required.");
                return View(announcement);
            }

            announcement.Title = title;
            announcement.Description = description;

            _context.Update(announcement);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        // POST: /Announcement/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(nameof(Index));
            }

            var announcement = await _context.Reports.FindAsync(id);
            if (announcement != null)
            {
                _context.Reports.Remove(announcement);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Manage));
        }
    }
}