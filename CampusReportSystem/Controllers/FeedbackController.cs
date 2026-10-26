using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;
using CampusReportSystem.Models;

namespace CampusReportSystem.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedbackController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Feedback/Index (Public feed for all campus members)
        [HttpGet]
        public async Task<IActionResult> Index(string? statusFilter)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Feedbacks.Include(f => f.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                // Explicit filter selection (e.g. statusFilter="Reviewed")
                query = query.Where(f => f.Status == statusFilter);
            }
            else
            {
                // Default Public Feed: STRICTLY show "Reviewed" posts.
                // Excludes "Pending" and "Dismissed" items automatically.
                query = query.Where(f => f.Status == "Reviewed");
            }

            var feedbacks = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
            ViewData["CurrentStatus"] = statusFilter;

            return View(feedbacks);
        }

        // POST: Feedback/React (AJAX Reaction for Members/Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> React(int id, string type)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Please log in to react." });
            }

            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return Json(new { success = false, message = "Feedback record not found." });
            }

            if (type == "like")
            {
                feedback.LikesCount++;
            }
            else if (type == "dislike")
            {
                feedback.DislikesCount++;
            }
            else
            {
                return Json(new { success = false, message = "Invalid reaction type." });
            }

            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                likes = feedback.LikesCount,
                dislikes = feedback.DislikesCount
            });
        }

        // GET: Feedback/Submit
        [HttpGet]
        public IActionResult Submit()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // POST: Feedback/SubmitAjax (AJAX Submission)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAjax([FromBody] Feedback feedbackModel)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Session expired. Please log in again." });
            }

            if (ModelState.IsValid)
            {
                feedbackModel.UserId = userId.Value;
                feedbackModel.CreatedAt = DateTime.Now;
                feedbackModel.Status = "Pending";

                _context.Feedbacks.Add(feedbackModel);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Thank you! Your feedback has been submitted successfully." });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(" ", errors) });
        }

        // GET: Feedback/History (User Side)
        [HttpGet]
        public async Task<IActionResult> History()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var feedbacks = await _context.Feedbacks
                .Where(f => f.UserId == userId.Value)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return View(feedbacks);
        }

        // GET: Feedback/AdminIndex (Admin Side)
        [HttpGet]
        public async Task<IActionResult> AdminIndex(string? statusFilter)
        {
            string? role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Feedbacks.Include(f => f.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(f => f.Status == statusFilter);
            }

            var feedbacks = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
            ViewData["CurrentStatus"] = statusFilter;

            return View(feedbacks);
        }

        // POST: Feedback/UpdateResponse (Admin AJAX Update)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateResponse(int id, string status, string? response)
        {
            string? role = HttpContext.Session.GetString("UserRole");
            if (role != "Admin")
            {
                return Json(new { success = false, message = "Unauthorized access." });
            }

            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return Json(new { success = false, message = "Feedback record not found." });
            }

            // Normalizes status string values to "Reviewed" or "Dismissed"
            feedback.Status = status;
            feedback.AdminResponse = response;

            _context.Feedbacks.Update(feedback);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Feedback status updated to '{status}' successfully!" });
        }
    }
}