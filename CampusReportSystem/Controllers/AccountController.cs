using Microsoft.AspNetCore.Mvc;
using CampusReportSystem.Data;
using CampusReportSystem.Models;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Identity;

namespace CampusReportSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AccountController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ==================== LOGIN ====================

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            var model = new LoginViewModel();

            // Check for saved Remember Me Cookie
            if (Request.Cookies.TryGetValue("RememberMeEmail", out string? rememberedEmail))
            {
                model.Email = rememberedEmail;
                model.RememberMe = true;
            }

            return View(model);
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Find User
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user != null)
            {
                bool isPasswordValid = false;
                var passwordHasher = new PasswordHasher<User>();

                // 2. Try verifying hashed password
                try
                {
                    var result = passwordHasher.VerifyHashedPassword(user, user.Password, model.Password);
                    if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        isPasswordValid = true;
                    }
                }
                catch
                {
                    // Fallback if password format in DB is plain-text
                }

                // 3. Fallback check for unhashed plain-text passwords
                if (!isPasswordValid && user.Password == model.Password)
                {
                    isPasswordValid = true;

                    // Automatically convert plain-text password to secure hash in DB
                    user.Password = passwordHasher.HashPassword(user, model.Password);
                    await _context.SaveChangesAsync();
                }

                if (!isPasswordValid)
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    ViewBag.ShowForgotPassword = true;
                    return View(model);
                }

                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is deactivated. Please contact an administrator.");
                    return View(model);
                }

                // Save session details
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserEmail", user.Email);
                HttpContext.Session.SetString("UserName", user.Name ?? "");
                HttpContext.Session.SetString("UserRole", user.Role ?? "Member");

                // Handle Remember Me Cookie
                if (model.RememberMe)
                {
                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(30),
                        HttpOnly = true,
                        IsEssential = true
                    };
                    Response.Cookies.Append("RememberMeEmail", model.Email, cookieOptions);
                }
                else
                {
                    Response.Cookies.Delete("RememberMeEmail");
                }

                // Redirect based on role
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Home");
                }

                return RedirectToAction("Index", "Home");
            }

            // Email not found or invalid credentials
            ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your email and password.");
            return View(model);
        }

        // ==================== REGISTER ====================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Send Email Verification Link (AJAX)
        [HttpPost]
        public async Task<IActionResult> SendEmailVerificationLink(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Please enter a valid email address!" });
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null || email == "admin@gmail.com")
            {
                return Json(new { success = false, message = "This email address has already been registered!" });
            }

            string verificationToken = Guid.NewGuid().ToString();

            HttpContext.Session.SetString("EmailVerificationToken", verificationToken);
            HttpContext.Session.SetString("Register_Email", email);
            HttpContext.Session.SetString("IsEmailVerified", "false");

            var verifyUrl = Url.Action("VerifyEmail", "Account", new { token = verificationToken, email = email }, Request.Scheme);

            try
            {
                var smtpSection = _config.GetSection("SmtpSettings");
                var message = new MimeMessage();

                message.From.Add(MailboxAddress.Parse(smtpSection["SenderEmail"]!));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = "Campus Report System - Verify Your Email";

                message.Body = new TextPart("html")
                {
                    Text = $@"
                        <h3>Email Verification</h3>
                        <p>Please click the button below to verify your email address:</p>
                        <p>
                            <a href='{verifyUrl}' style='background-color: #0d6efd; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Verify Email</a>
                        </p>
                        <p>Or copy this link to your browser:</p>
                        <p><a href='{verifyUrl}'>{verifyUrl}</a></p>"
                };

                using var client = new MailKit.Net.Smtp.SmtpClient();
                await client.ConnectAsync(
                    smtpSection["Server"]!,
                    int.Parse(smtpSection["Port"] ?? "587"),
                    MailKit.Security.SecureSocketOptions.StartTls
                );
                await client.AuthenticateAsync(
                    smtpSection["SenderEmail"]!,
                    smtpSection["Password"]!
                );
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return Json(new { success = true, message = "Verification email sent! Please check your inbox and click the link." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Failed to send email: " + ex.Message });
            }
        }

        // Verify Email Endpoint
        [HttpGet]
        public IActionResult VerifyEmail(string token, string email)
        {
            string? savedToken = HttpContext.Session.GetString("EmailVerificationToken");
            string? savedEmail = HttpContext.Session.GetString("Register_Email");

            if (!string.IsNullOrEmpty(savedToken) && savedToken == token && savedEmail == email)
            {
                HttpContext.Session.SetString("IsEmailVerified", "true");
                TempData["SuccessMessage"] = "Email verified successfully! You can now complete your registration.";
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid or expired verification link!";
            }

            return RedirectToAction("Register");
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, int captchaAnswer, int? userCaptcha)
        {

            if (!userCaptcha.HasValue || userCaptcha.Value != captchaAnswer)
            {
                ModelState.AddModelError(string.Empty, "Incorrect security verification code. Please try again.");
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Clear Session
            HttpContext.Session.Remove("EmailVerificationToken");
            HttpContext.Session.Remove("Register_Email");
            HttpContext.Session.Remove("IsEmailVerified");

            // 1. Check duplicate email
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());

            if (existingUser != null || model.Email.ToLower() == "admin@gmail.com")
            {
                ModelState.AddModelError("Email", "This email address has already been registered!");
                return View(model);
            }

            //Next RoleSeqNo for new user
            int nextSeq = 1;
            var lastMember = await _context.Users
                .Where(u => u.Role == "User" || u.Role == "Member")
                .OrderByDescending(u => u.RoleSeqNo)
                .FirstOrDefaultAsync();

            if (lastMember != null)
                nextSeq = lastMember.RoleSeqNo + 1;

            // 2. Create User
            var newUser = new User
            {
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber ?? "",
                Role = "User",
                RoleSeqNo = nextSeq,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var hasher = new PasswordHasher<User>();
            newUser.Password = hasher.HashPassword(newUser, model.Password);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        // ==================== PASSWORD RESET ====================

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Please enter your email address.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "This email address is not registered.");
                return View();
            }

            return RedirectToAction("ResetPassword", new { email = user.Email });
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            ViewBag.Email = email;
            return View();
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError(string.Empty, "Password fields cannot be empty.");
                ViewBag.Email = email;
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                ViewBag.Email = email;
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user != null)
            {
                var hasher = new PasswordHasher<User>();
                user.Password = hasher.HashPassword(user, newPassword);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password reset successfully! Please login with your new password.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, "User not found.");
            ViewBag.Email = email;
            return View();
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }
}
