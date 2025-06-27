using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechWorld.Models;
using TechWorld.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TechWorld.Data;
using TechWorld.Models.ViewModels;

namespace TechWorld.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger, IShoppingCartService shoppingCartService, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _shoppingCartService = shoppingCartService;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> Addresses()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var addresses = await _context.UserAddresses
                .Where(a => a.UserID == userId)
                .OrderByDescending(a => a.AddedAt)
                .ToListAsync();

            return View(addresses);
        }

        // GET: Account/Login
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserName == model.UserName || u.Email == model.UserName);

                    if (user != null && VerifyPassword(model.Password, user.Password))
                    {
                        if (user.IsTwoFactorEnabled)
                        {
                            // Gửi OTP 2FA nếu bật
                            var otp = GenerateOTP();
                            HttpContext.Session.SetString($"2FA_OTP_{user.Email}", otp);
                            HttpContext.Session.SetString($"2FA_OTPExpiry_{user.Email}", DateTime.Now.AddMinutes(15).ToString());
                            HttpContext.Session.SetString($"2FA_UserId", user.UserID.ToString());
                            HttpContext.Session.SetString($"2FA_ReturnUrl", returnUrl ?? string.Empty);
                            HttpContext.Session.SetString($"2FA_RememberMe", model.RememberMe.ToString());

                            var subject = "Mã OTP Xác Thực Đăng Nhập - TechWorld";
                            var message = $@"
                                <html>
                                    <head>
                                        <style>
                                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                            .header {{ background-color: #0f07be; color: white; padding: 10px; text-align: center; }}
                                            .content {{ padding: 20px; border: 1px solid #ddd; }}
                                            .otp {{ font-size: 24px; font-weight: bold; text-align: center; margin: 20px 0; letter-spacing: 5px; }}
                                            .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                                        </style>
                                    </head>
                                    <body>
                                        <div class='container'>
                                            <div class='header'>
                                                <h2>TechWorld - Xác thực 2 lớp</h2>
                                            </div>
                                            <div class='content'>
                                                <p>Xin chào,</p>
                                                <p>Chúng tôi nhận được yêu cầu đăng nhập tài khoản của bạn. Vui lòng sử dụng mã OTP dưới đây để xác minh tài khoản:</p>
                                                <div class='otp'>{otp}</div>
                                                <p>Mã này có hiệu lực trong 15 phút. Nếu bạn không yêu cầu đăng nhập, vui lòng bỏ qua email này.</p>
                                                <p>Trân trọng,<br>Đội ngũ TechWorld</p>
                                            </div>
                                            <div class='footer'>
                                                <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                                            </div>
                                        </div>
                                    </body>
                                </html>";

                            await _emailService.SendEmailAsync(user.Email, subject, message);

                            return View("Verify2FA", new Verify2FAViewModel { Email = user.Email });
                        }

                        // Đăng nhập trực tiếp nếu không bật 2FA
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.UserName),
                            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, user.Role ?? "Customer"),
                            new Claim("Phone", user.Phone ?? "")
                        };

                        var claimsIdentity = new ClaimsIdentity(
                            claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = model.RememberMe,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        _logger.LogInformation("User {UserName} logged in at {Time}.", user.UserName, DateTime.UtcNow);

                        await _shoppingCartService.MergeAnonymousCartAsync(User, HttpContext.Session);

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng.");
                        return View(model);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during login for user {UserName}", model.UserName);
                    ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng nhập. Vui lòng thử lại sau.");
                }
            }

            return View(model);
        }
        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // GET: Account/Verify2FA
        public IActionResult Verify2FA()
        {
            return View(new Verify2FAViewModel());
        }

        // POST: Account/Verify2FA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(Verify2FAViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var storedOTP = HttpContext.Session.GetString($"2FA_OTP_{model.Email}");
            var expiryString = HttpContext.Session.GetString($"2FA_OTPExpiry_{model.Email}");
            var userIdString = HttpContext.Session.GetString($"2FA_UserId");
            var returnUrl = HttpContext.Session.GetString($"2FA_ReturnUrl");
            var rememberMeString = HttpContext.Session.GetString($"2FA_RememberMe");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString) || string.IsNullOrEmpty(userIdString))
            {
                _logger.LogWarning("2FA verification failed for {Email}: Session data missing.", model.Email);
                // Lỗi chung, không phải lỗi nhập liệu của người dùng
                ModelState.AddModelError(string.Empty, "Phiên xác thực đã hết hạn. Vui lòng đăng nhập lại.");
                return View(model);
            }

            if (!DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn. Vui lòng đăng nhập lại để nhận mã mới.");
                // Xóa session OTP đã hết hạn
                HttpContext.Session.Remove($"2FA_OTP_{model.Email}");
                HttpContext.Session.Remove($"2FA_OTPExpiry_{model.Email}");
                HttpContext.Session.Remove($"2FA_UserId");
                HttpContext.Session.Remove($"2FA_ReturnUrl");
                HttpContext.Session.Remove($"2FA_RememberMe");
                return View(model);
            }

            if (model.OTP != storedOTP)
            {
                ModelState.AddModelError("OTP", "Mã OTP không chính xác. Vui lòng thử lại.");
                return View(model);
            }

            // Xác minh thành công, đăng nhập người dùng
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == int.Parse(userIdString));
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy người dùng. Vui lòng thử lại.");
                return RedirectToAction("Login");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "Customer"),
                new Claim("Phone", user.Phone ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = bool.Parse(rememberMeString ?? "false"),
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("User {UserName} completed 2FA login at {Time}.", user.UserName, DateTime.UtcNow);

            await _shoppingCartService.MergeAnonymousCartAsync(User, HttpContext.Session);

            // Xóa session OTP
            HttpContext.Session.Remove($"2FA_OTP_{model.Email}");
            HttpContext.Session.Remove($"2FA_OTPExpiry_{model.Email}");
            HttpContext.Session.Remove($"2FA_UserId");
            HttpContext.Session.Remove($"2FA_ReturnUrl");
            HttpContext.Session.Remove($"2FA_RememberMe");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/TwoFactorSettings
        [Authorize]
        public async Task<IActionResult> TwoFactorSettings()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            var viewModel = new TwoFactorSettingsViewModel
            {
                IsTwoFactorEnabled = user.IsTwoFactorEnabled
            };

            ViewBag.FullName = user.FullName;
            ViewBag.Email = user.Email;
            ViewBag.Points = user.Points ?? 0;
            ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";

            return View(viewModel);
        }

        // POST: Account/TwoFactorSettings
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TwoFactorSettings(TwoFactorSettingsViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.FullName = user.FullName;
                ViewBag.Email = user.Email;
                ViewBag.Points = user.Points ?? 0;
                ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";
                return View(model);
            }

            try
            {
                user.IsTwoFactorEnabled = model.IsTwoFactorEnabled;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = model.IsTwoFactorEnabled
                    ? "Đã bật xác thực hai lớp thành công!"
                    : "Đã tắt xác thực hai lớp thành công!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating 2FA settings for user {UserId}", userId);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cập nhật cài đặt xác thực hai lớp. Vui lòng thử lại sau.";
                ViewBag.FullName = user.FullName;
                ViewBag.Email = user.Email;
                ViewBag.Points = user.Points ?? 0;
                ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";
                return View(model);
            }
        }


        // POST: Account/VerifyOTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var storedOTP = HttpContext.Session.GetString($"OTP_{model.Email}");
            var expiryString = HttpContext.Session.GetString($"OTPExpiry_{model.Email}");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString))
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã mới.");
                return View(model);
            }

            if (!DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                ViewBag.ErrorMessage = "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.";
                HttpContext.Session.Remove($"OTP_{model.Email}");
                HttpContext.Session.Remove($"OTPExpiry_{model.Email}");
                return View(model);
            }

            if (model.OTP != storedOTP)
            {
                ModelState.AddModelError("OTP", "Mã OTP không chính xác. Vui lòng thử lại.");
                return View(model);
            }

            return View("ResetPassword", new ResetPasswordViewModel { Email = model.Email, OTP = model.OTP });
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            _logger.LogInformation("Password reset process started for email {Email}", model.Email);

            var storedOTP = HttpContext.Session.GetString($"OTP_{model.Email}");
            var expiryString = HttpContext.Session.GetString($"OTPExpiry_{model.Email}");

            if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryString) ||
                model.OTP != storedOTP || !DateTime.TryParse(expiryString, out var expiry) || expiry < DateTime.Now)
            {
                _logger.LogWarning("Invalid or expired OTP session for password reset attempt for email {Email}. OTP provided was incorrect or session expired.", model.Email);
                TempData["ErrorMessage"] = "Phiên làm việc đã hết hạn hoặc mã OTP không chính xác. Vui lòng yêu cầu mã OTP mới.";
                return RedirectToAction("ForgotPassword");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                _logger.LogWarning("Password reset attempt for a non-existent email account: {Email}", model.Email);
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản. Vui lòng thử lại.";
                return RedirectToAction("ForgotPassword");
            }

            try
            {
                user.Password = HashPassword(model.NewPassword);
                _context.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password successfully reset for user {UserId} with email {Email}.", user.UserID, user.Email);

                HttpContext.Session.Remove($"OTP_{model.Email}");
                HttpContext.Session.Remove($"OTPExpiry_{model.Email}");

                TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for email {Email}", model.Email);
                ViewBag.ErrorMessage = "Đã xảy ra lỗi khi đặt lại mật khẩu. Vui lòng thử lại sau.";
                return View(model);
            }
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Models.RegisterViewModel model)
        {
            try
            {
                // Debug: Kiểm tra ModelState
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState không hợp lệ: {Errors}",
                        string.Join(", ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)));

                    return View(model);
                }

                // Kiểm tra tên đăng nhập đã tồn tại
                var userNameExists = await _context.Users.AnyAsync(u => u.UserName == model.UserName);
                if (userNameExists)
                {
                    ModelState.AddModelError("UserName", "Tên đăng nhập đã tồn tại.");
                    return View(model);
                }

                // Kiểm tra email đã tồn tại
                var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Email đã được sử dụng bởi tài khoản khác.");
                    return View(model);
                }

                // Lấy rank mặc định (rank có điểm thấp nhất)
                var defaultRank = await _context.Ranks
                    .OrderBy(r => r.MinimumPoints)
                    .FirstOrDefaultAsync();

                var user = new User
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FullName = model.FullName,
                    Password = HashPassword(model.Password),
                    Phone = model.Phone,
                    Address = model.Address, // Có thể null
                    Role = "Customer", // Sửa từ "User" thành "Customer"
                    CreatedAt = DateTime.Now,
                    Points = 0,
                    RankID = defaultRank?.RankID // Gán rank mặc định
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserName} created a new account at {Time}.", user.UserName, DateTime.UtcNow);

                // Đăng nhập người dùng sau khi đăng ký
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("Phone", user.Phone ?? "")
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                await _shoppingCartService.MergeAnonymousCartAsync(User, HttpContext.Session);

                TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Chào mừng bạn đến với cửa hàng.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user {UserName}: {Message}", model.UserName, ex.Message);
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng ký: " + ex.Message);
                return View(model);
            }
        }

        // GET: Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "Đăng xuất thành công!";
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user == null)
                {
                    ModelState.AddModelError("Email", "Email này không tồn tại trong hệ thống.");
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", model.Email);
                    return View(model);
                }

                // Tạo mã OTP
                var otp = GenerateOTP();
                HttpContext.Session.SetString($"OTP_{model.Email}", otp);
                HttpContext.Session.SetString($"OTPExpiry_{model.Email}", DateTime.Now.AddMinutes(15).ToString());

                // Gửi email với mã OTP
                var subject = "Mã OTP Đặt Lại Mật Khẩu - TechWorld";
                var message = $@"
                    <html>
                        <head>
                            <style>
                                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                .header {{ background-color: #0f07be; color: white; padding: 10px; text-align: center; }}
                                .content {{ padding: 20px; border: 1px solid #ddd; }}
                                .otp {{ font-size: 24px; font-weight: bold; text-align: center; margin: 20px 0; letter-spacing: 5px; }}
                                .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <div class='header'>
                                    <h2>TechWorld - Đặt lại mật khẩu</h2>
                                </div>
                                <div class='content'>
                                    <p>Xin chào,</p>
                                    <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Vui lòng sử dụng mã OTP dưới đây để xác minh:</p>
                                    <div class='otp'>{otp}</div>
                                    <p>Mã này có hiệu lực trong 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                                    <p>Trân trọng,<br>Đội ngũ MyKitDum</p>
                                </div>
                                <div class='footer'>
                                    <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                                </div>
                            </div>
                        </body>
                    </html>";

                await _emailService.SendEmailAsync(model.Email, subject, message);

                ViewBag.SuccessMessage = "Mã OTP đã được gửi đến email của bạn.";
                return View("VerifyOTP", new VerifyOTPViewModel { Email = model.Email });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending OTP for email {Email}", model.Email);
                ViewBag.ErrorMessage = "Đã xảy ra lỗi khi gửi mã OTP. Vui lòng thử lại sau.";
                return View(model);
            }
        }

        // GET: Account/Profile
        public async Task<IActionResult> Profile()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users
                    .Include(u => u.Rank)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (user == null)
                {
                    return NotFound();
                }

                int currentPoints = user.Points ?? 0;

                // Logic cập nhật Rank tự động dựa trên điểm
                var newRank = await _context.Ranks
                    .Where(r => r.MinimumPoints <= currentPoints)
                    .OrderByDescending(r => r.MinimumPoints)
                    .FirstOrDefaultAsync();

                if (newRank != null && (user.RankID == null || user.RankID != newRank.RankID))
                {
                    _logger.LogInformation("User {UserId} rank changed from {OldRankName} (ID: {OldRankId}) to {NewRankName} (ID: {NewRankId}) with {Points} points.",
                                           userId,
                                           user.Rank?.RankName ?? "No Rank",
                                           user.RankID,
                                           newRank.RankName,
                                           newRank.RankID,
                                           currentPoints);
                    user.RankID = newRank.RankID;
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                    user.Rank = newRank;
                }

                ViewBag.Points = currentPoints;
                ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";

                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin tài khoản. Vui lòng thử lại sau.";
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Account/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(User model)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                if (model.UserID != userId)
                {
                    return Forbid();
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                // Kiểm tra xem email đã tồn tại chưa (nếu thay đổi)
                if (user.Email != model.Email)
                {
                    var emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserID != userId);
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "Email này đã được sử dụng bởi tài khoản khác.");
                        return View("Profile", model);
                    }
                }

                // Cập nhật thông tin người dùng
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.Phone = model.Phone;
                user.Address = model.Address;

                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật thông tin tài khoản thành công!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", model.UserID);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cập nhật thông tin tài khoản. Vui lòng thử lại sau.";
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var currentUser = await _context.Users.Include(u => u.Rank).FirstOrDefaultAsync(u => u.UserID == userId);
                ViewBag.Points = currentUser?.Points ?? 0;
                ViewBag.RankName = currentUser?.Rank?.RankName ?? "Chưa có hạng";
                return View("Profile", model);
            }
        }

        // GET: Account/ChangePassword
        public async Task<IActionResult> ChangePassword()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            ViewBag.FullName = user.FullName;
            ViewBag.Email = user.Email;
            ViewBag.Points = user.Points ?? 0;
            ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";

            return View();
        }

        // POST: Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _context.Users.FindAsync(userId);

                    if (user == null)
                    {
                        return NotFound();
                    }

                    // Kiểm tra mật khẩu hiện tại
                    if (!VerifyPassword(model.CurrentPassword, user.Password))
                    {
                        ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng.");
                        await SetSidebarViewBagData(userId);
                        return View(model);
                    }

                    // Cập nhật mật khẩu mới
                    user.Password = HashPassword(model.NewPassword);
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                    return RedirectToAction("Profile");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error changing password");
                    ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi đổi mật khẩu. Vui lòng thử lại sau.");
                    await SetSidebarViewBagData(userId);
                }
            }
            else
            {
                await SetSidebarViewBagData(userId);
            }

            return View(model);
        }

        private async Task SetSidebarViewBagData(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Rank)
                .FirstOrDefaultAsync(u => u.UserID == userId);


            if (user != null)
            {
                ViewBag.FullName = user.FullName;
                ViewBag.Email = user.Email;
                ViewBag.Points = user.Points ?? 0;
                ViewBag.RankName = user.Rank?.RankName ?? "Chưa có hạng";
            }
            else
            {
                ViewBag.FullName = "Người dùng";
                ViewBag.Email = "";
                ViewBag.Points = 0;
                ViewBag.RankName = "Chưa có hạng";
            }
        }

        // Phương thức mã hóa mật khẩu
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Phương thức xác minh mật khẩu
        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == hashedPassword;
        }

        [Authorize]
        public async Task<IActionResult> Wishlist()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }
            var currentUser = await _context.Users
                .Include(u => u.Rank) // Bao gồm thông tin Rank
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (currentUser == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            ViewBag.FullName = currentUser.FullName;
            ViewBag.Email = currentUser.Email;
            ViewBag.Points = currentUser.Points ?? 0;
            ViewBag.RankName = currentUser.Rank?.RankName ?? "Chưa có hạng";

            var wishlist = await _context.WishLists
                .Where(w => w.UserID == userId)
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductImages)
                .ToListAsync();

            return View(wishlist);
        }

        [Authorize]
        public async Task<IActionResult> Voucher()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("Không tìm thấy thông tin người dùng.");
            }

            var userRankId = user.RankID;

            var availableVouchers = await _context.Promotions
                .Where(p => p.ProductID == null && p.DiscountPercentage == 0m && p.IsActive && p.EndDate > DateTime.Now && (p.RankID == null || p.RankID == userRankId))
                .OrderByDescending(p => p.EndDate)
                .ToListAsync();

            ViewBag.FullName = user.FullName;
            ViewBag.Email = user.Email;
            ViewBag.Points = user.Points ?? 0;
            var rank = await _context.Ranks.FindAsync(user.RankID);
            ViewBag.RankName = rank?.RankName ?? "Chưa có hạng";

            return View(availableVouchers);
        }

        [Authorize]
        public async Task<IActionResult> Reviews()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var reviews = await _context.Reviews
                .Include(r => r.Product)
                    .ThenInclude(p => p.ProductImages)
                .Where(r => r.UserID == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }
    }

    // View Models
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự và tối đa {1} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và xác nhận mật khẩu không khớp.")]
        public string ConfirmPassword { get; set; }
    }

    public class TwoFactorSettingsViewModel
    {
        [Display(Name = "Bật xác thực hai lớp")]
        public bool IsTwoFactorEnabled { get; set; }
    }
}

namespace TechWorld.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        [StringLength(20, ErrorMessage = "Tên đăng nhập phải có ít nhất {2} ký tự và tối đa {1} ký tự.", MinimumLength = 3)]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ và tên")]
        [StringLength(100, ErrorMessage = "Họ tên phải có ít nhất {2} ký tự và tối đa {1} ký tự.", MinimumLength = 2)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        [StringLength(100)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        [StringLength(15)]
        public string Phone { get; set; }

        [Display(Name = "Địa chỉ")]
        [StringLength(255)]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự và tối đa {1} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu và xác nhận mật khẩu không khớp.")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Tôi đồng ý với điều khoản dịch vụ")]
        [Required(ErrorMessage = "Bạn phải đồng ý với điều khoản dịch vụ để đăng ký.")]
        public bool AgreeTerms { get; set; }
    }

    public class Verify2FAViewModel
    {
        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mã OTP là bắt buộc")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 ký tự")]
        public string OTP { get; set; }
    }
}
