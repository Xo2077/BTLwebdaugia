using Microsoft.AspNetCore.Mvc;
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using Microsoft.AspNetCore.Identity;
using BTLwebdaugia.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;




namespace BTLwebdaugia.Controllers
{
    public class AccountController : Controller
    {
        private readonly BidHubDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AccountController(BidHubDbContext context)
        {
            _context = context;
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignIn(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignIn(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string email = model.Email.Trim();
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            // Do not reveal whether the account or password is incorrect.
            if (user is null ||
                _passwordHasher.VerifyHashedPassword(
                    user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                properties);

            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Chuẩn hóa Email trước khi kiểm tra.
            string email = model.Email.Trim();

            // Kiểm tra Email đã tồn tại trong bảng Users.
            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered."
                );

                return View(model);
            }

            // Tạo Username tự động từ họ và tên.
            string username = await GenerateUniqueUsernameAsync(
                model.FirstName,
                model.LastName);


            var user = new User
            {
                Username = username,
                Email = email,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            // Băm mật khẩu trước khi lưu.
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.Password);


            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    _context.Users.Add(user);

                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(SignIn));
                }
                catch (DbUpdateException ex)
                    when (ex.InnerException is SqlException sqlEx &&
                          (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    // Không giữ Entity ở trạng thái Added
                    // sau khi thao tác INSERT thất bại.
                    _context.Entry(user).State = EntityState.Detached;

                    // Kiểm tra Email có bị đăng ký bởi request khác không.
                    bool emailTaken = await _context.Users
                        .AnyAsync(u => u.Email == email);

                    if (emailTaken)
                    {
                        ModelState.AddModelError(
                            nameof(model.Email),
                            "This email is already registered."
                        );

                        return View(model);
                    }

                    // Kiểm tra Username có bị trùng không.
                    bool usernameTaken = await _context.Users
                        .AnyAsync(u => u.Username == user.Username);

                    if (!usernameTaken)
                    {
                        // Có lỗi Unique khác nhưng chưa xác định được.
                        // Không đoán rằng đó là lỗi Username.
                        throw;
                    }

                    if (attempt == 3)
                    {
                        break;
                    }

                    // Tìm Username mới và thử lưu lại.
                    user.Username = await GenerateUniqueUsernameAsync(
                        model.FirstName,
                        model.LastName);
                }
            }

            ModelState.AddModelError(
                string.Empty,
                "Registration could not be completed. Please try again."
            );

            return View(model);

        }



        [HttpGet]
        public IActionResult Profile()
        {
            return View();
        }

        [HttpGet]
        public IActionResult LogOut()
        {
            // Opening a URL must not log users out.
            return RedirectToAction(nameof(SignIn));
        }

        [HttpPost]
        [ActionName("LogOut")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOutPost()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(SignIn));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Content("Access denied. Your account does not have permission to access this page.");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }



        private static string GenerateBaseUsername(
            string firstName,
            string lastName)
        {
            string fullName = $"{firstName}{lastName}"
                .Trim()
                .ToLowerInvariant()
                .Replace('đ', 'd');

            string normalized = fullName.Normalize(
                NormalizationForm.FormD);

            var builder = new StringBuilder();

            foreach (char character in normalized)
            {
                UnicodeCategory category =
                    CharUnicodeInfo.GetUnicodeCategory(character);

                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if ((character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9'))
                {
                    builder.Append(character);
                }
            }

            string username = builder.ToString();

            if (string.IsNullOrEmpty(username))
            {
                username = "user";
            }

            if (username.Length > 50)
            {
                username = username[..50];
            }

            return username;
        }


        private async Task<string> GenerateUniqueUsernameAsync(
            string firstName,
            string lastName)
        {
            string baseUsername = GenerateBaseUsername(
                firstName,
                lastName);

            string username = baseUsername;
            int suffix = 2;

            while (await _context.Users
                .AnyAsync(u => u.Username == username))
            {
                string suffixText = suffix.ToString();

                int maxBaseLength = 50 - suffixText.Length;

                string shortenedBase = baseUsername.Length > maxBaseLength
                    ? baseUsername[..maxBaseLength]
                    : baseUsername;

                username = shortenedBase + suffixText;
                suffix++;
            }

            return username;
        }




    }
}
