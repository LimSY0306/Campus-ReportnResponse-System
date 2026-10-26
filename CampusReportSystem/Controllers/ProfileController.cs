using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;
using CampusReportSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace CampusReportSystem.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Profile/Index
        public async Task<IActionResult> Index()
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your profile.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == sessionUserId.Value);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Profile/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null || id != sessionUserId)
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Profile/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,PhoneNumber,Gender,BirthDate")] User updatedUser, IFormFile? profilePicture)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != updatedUser.Id || id != sessionUserId)
            {
                return Unauthorized();
            }

            // Clear validation for fields not passed in the edit form
            ModelState.Remove("Password");
            ModelState.Remove("Role");
            ModelState.Remove("ProfilePicturePath");

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser == null)
                {
                    return NotFound();
                }

                // Update text and selection properties
                existingUser.Name = updatedUser.Name;
                existingUser.Email = updatedUser.Email;
                existingUser.PhoneNumber = updatedUser.PhoneNumber;
                existingUser.Gender = updatedUser.Gender;
                existingUser.BirthDate = updatedUser.BirthDate;

                // Handle standard file upload (fallback)
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(profilePicture.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(fileStream);
                    }

                    existingUser.ProfilePicturePath = "/uploads/" + uniqueFileName;
                }

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                // Update session state for navbar display
                HttpContext.Session.SetString("UserName", existingUser.Name);

                TempData["SuccessMessage"] = "Profile updated successfully!";

                return RedirectToAction(nameof(Index));
            }

            return View(updatedUser);
        }

        // POST: Profile/UploadProfilePicture (AJAX for Cropper.js)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture([FromBody] ImageUploadModel model)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return Json(new { success = false, message = "Session expired. Please log in again." });
            }

            var user = await _context.Users.FindAsync(sessionUserId.Value);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            if (string.IsNullOrEmpty(model?.Base64Image))
            {
                return Json(new { success = false, message = "Invalid image data." });
            }

            try
            {
                var base64Data = model.Base64Image.Contains(",") 
                    ? model.Base64Image.Split(',')[1] 
                    : model.Base64Image;

                byte[] imageBytes = Convert.FromBase64String(base64Data);

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string fileName = $"profile_{sessionUserId.Value}_{DateTime.Now.Ticks}.png";
                string filePath = Path.Combine(uploadsFolder, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

                user.ProfilePicturePath = $"/uploads/profiles/{fileName}";
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, imagePath = user.ProfilePicturePath, message = "Profile picture updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    public class ImageUploadModel
    {
        public string Base64Image { get; set; } = string.Empty;
    }
}