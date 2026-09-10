using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using Shelfy.Data;
using Shelfy.Models;
using Shelfy.Services;

namespace Shelfy.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private const int VerificationCodeMinutes = 10;
        private const int MaxTwoFactorAttempts = 3;

        public AccountController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        public async Task<IActionResult> FinePayment()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {

                return RedirectToAction("Login");
            }

            var fines = await _context.GetUserFinesAsync(userId.Value, 0);
            ViewBag.TotalFines = fines;
            ViewBag.Tax = fines * 0.24m;
            ViewBag.GrandTotal = fines * 1.24m;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var result = await _context.ClearUserFinesAsync(userId.Value);
            return Json(
                new
                {
                    success = result,
                    message = result
                        ? "Payment successful. All fines have been cleared!"
                        : "Transaction failed."
                }
            );
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {


                var user = await _context.LoginUserAsync(
                    model.Email,
                    HashingAlgorithm.ComputeSha256(model.Password)
                );
                if (user != null)
                {
                    if (user.TwoFactorAuth && _emailService.IsConfigured)
                    {
                        var code = GenerateCode();
                        HttpContext.Session.SetInt32("PendingUserId", user.UserId);
                        HttpContext.Session.SetString("PendingTwoFactorCode", code);
                        HttpContext.Session.SetString(
                            "PendingTwoFactorExpires",
                            DateTime.UtcNow.AddMinutes(VerificationCodeMinutes).ToString("O")
                        );
                        HttpContext.Session.SetInt32("TwoFactorAttempts", 0);

                        await _emailService.SendTwoFactorCodeAsync(user.Email, user.Username, code);
                        return RedirectToAction("VerifyTwoFactor");
                    }

                    await CompleteSignInAsync(user);
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
            return View(model);
        }





        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }




        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _context.RegisterUserAsync(
                    model.UserName,
                    model.Email,
                    HashingAlgorithm.ComputeSha256(model.Password),
                    0
                );
                if (result)
                {

                    await _emailService.SendWelcomeEmailAsync(model.Email, model.UserName);
                    return RedirectToAction("Login");
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Email already exists or registration failed."
                );
            }
            return View(model);
        }


        [HttpGet]
        public IActionResult VerifyTwoFactor()
        {
            if (!HttpContext.Session.GetInt32("PendingUserId").HasValue)
            {
                return RedirectToAction("Login");
            }

            return View(new TwoFactorViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> VerifyTwoFactor(TwoFactorViewModel model)
        {

            var pendingUserId = HttpContext.Session.GetInt32("PendingUserId");
            var expectedCode = HttpContext.Session.GetString("PendingTwoFactorCode");
            var expiresText = HttpContext.Session.GetString("PendingTwoFactorExpires");
            var attempts = HttpContext.Session.GetInt32("TwoFactorAttempts") ?? 0;

            if (
                !pendingUserId.HasValue
                || string.IsNullOrWhiteSpace(expectedCode)
                || string.IsNullOrWhiteSpace(expiresText)
            )
            {
                return RedirectToAction("Login");
            }

            if (!DateTime.TryParse(expiresText, out var expiresAt) || DateTime.UtcNow > expiresAt)
            {
                ClearPendingTwoFactor();
                ModelState.AddModelError(
                    string.Empty,
                    "The verification code expired. Please log in again."
                );
                return View(model);
            }
            

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Code != expectedCode)
            {
                attempts++;
                HttpContext.Session.SetInt32("TwoFactorAttempts", attempts);

                if (attempts >= MaxTwoFactorAttempts)
                {
                    ClearPendingTwoFactor();
                    ModelState.AddModelError(
                        string.Empty,
                        "Too many failed attempts. Login session locked. Please sign in again."
                    );
                    return View(model);
                }

                ModelState.AddModelError(string.Empty, "Invalid verification code.");
                return View(model);
            }

            var user = await _context.GetUserByIdAsync(pendingUserId.Value);
            if (user == null)
            {
                ClearPendingTwoFactor();
                return RedirectToAction("Login");
            }

            ClearPendingTwoFactor();
            await CompleteSignInAsync(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!_emailService.IsConfigured)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Email is not configured yet. Add your Gmail SMTP details first."
                );
                return View(model);
            }

            var user = await _context.GetUserByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "No account was found for that email address."
                );
                return View(model);
            }

            var code = GenerateCode();
            HttpContext.Session.SetString("PasswordResetEmail", user.Email);
            HttpContext.Session.SetString("PasswordResetCode", code);
            HttpContext.Session.SetString(
                "PasswordResetExpires",
                DateTime.UtcNow.AddMinutes(VerificationCodeMinutes).ToString("O")
            );

            await _emailService.SendPasswordResetCodeAsync(user.Email, user.Username, code);
            TempData["StatusMessage"] = "A reset code was sent to your email.";
            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View(
                new ResetPasswordViewModel
                {
                    Email = HttpContext.Session.GetString("PasswordResetEmail") ?? string.Empty
                }
            );
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var expectedEmail = HttpContext.Session.GetString("PasswordResetEmail");
            var expectedCode = HttpContext.Session.GetString("PasswordResetCode");
            var expiresText = HttpContext.Session.GetString("PasswordResetExpires");

            if (
                string.IsNullOrWhiteSpace(expectedEmail)
                || string.IsNullOrWhiteSpace(expectedCode)
                || string.IsNullOrWhiteSpace(expiresText)
            )
            {
                ModelState.AddModelError(string.Empty, "Please request a new password reset code.");
                return View(model);
            }

            if (!DateTime.TryParse(expiresText, out var expiresAt) || DateTime.UtcNow > expiresAt)
            {
                ClearPasswordReset();
                ModelState.AddModelError(
                    string.Empty,
                    "The reset code expired. Please request a new one."
                );
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (
                !string.Equals(model.Email, expectedEmail, StringComparison.OrdinalIgnoreCase)
                || model.Code != expectedCode
            )
            {
                ModelState.AddModelError(string.Empty, "Invalid reset details.");
                return View(model);
            }

            var user = await _context.GetUserByEmailAsync(expectedEmail);
            var newPassword = HashingAlgorithm.ComputeSha256(model.NewPassword);
            if (user == null || !await _context.UpdatePasswordAsync(user.UserId, newPassword))
            {
                ModelState.AddModelError(string.Empty, "Could not reset the password.");
                return View(model);
            }

            ClearPasswordReset();
            TempData["StatusMessage"] = "Your password has been reset. You can log in now.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> Info()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.GetUserByIdAsync(userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            var fines = await _context.GetUserFinesAsync(userId.Value, 0);

            return View(
                new AccountInfoViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    Admin = user.Admin,
                    TwoFactorAuth = user.TwoFactorAuth,
                    TotalFines = fines
                }
            );
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTwoFactor(bool enabled)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var result = await _context.ToggleTwoFactorAsync(userId.Value, enabled);
            return Json(
                new
                {
                    success = result,
                    message = result
                        ? $"2FA {(enabled ? "enabled" : "disabled")} successfully."
                        : "Failed to update 2FA settings."
                }
            );
        }

        private async Task CompleteSignInAsync(User user)
        {
            HttpContext.Session.SetString("UserName", user.Username);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetInt32("AdminLevel", user.Admin);
            await _emailService.SendLoginNotificationAsync(user.Email, user.Username);
        }

        private static string GenerateCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        private void ClearPendingTwoFactor()
        {
            HttpContext.Session.Remove("PendingUserId");
            HttpContext.Session.Remove("PendingTwoFactorCode");
            HttpContext.Session.Remove("PendingTwoFactorExpires");
        }

        private void ClearPasswordReset()
        {
            HttpContext.Session.Remove("PasswordResetEmail");
            HttpContext.Session.Remove("PasswordResetCode");
            HttpContext.Session.Remove("PasswordResetExpires");
        }
    }
}
