using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.Services;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Localization;

namespace BTLwebdaugia.Controllers
{
    public class AccountController : Controller
    {
        private readonly BidHubDbContext _context;
        private readonly IStringLocalizer<SharedLabels> _labels;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AccountController(
            BidHubDbContext context,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger,
            IStringLocalizer<SharedLabels> labels)
        {
            _context = context;
            _logger = logger;
            _labels = labels;
            _environment = environment;
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
        public async Task<IActionResult> SignIn(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email.Trim();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null ||
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.Password
                ) == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email or password.");

                return View(model);
            }

            var identity = CreateIdentity(user);

            var properties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);

            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignUp()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string username = model.Username.Trim();
            string email = model.Email.Trim();

            if (await AddSignUpConflictErrorsAsync(username, email))
                return View(model);

            var user = new User
            {
                Username = username,
                Email = email,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.Password);

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
                // Another registration may claim the username/email after the initial check.
                _context.Entry(user).State = EntityState.Detached;

                if (!await AddSignUpConflictErrorsAsync(username, email))
                    throw;

                return View(model);
            }
        }

        private async Task<bool> AddSignUpConflictErrorsAsync(string username, string email)
        {
            bool usernameExists = await _context.Users.AnyAsync(u => u.Username == username);
            bool emailExists = await _context.Users.AnyAsync(u => u.Email == email);

            if (usernameExists)
                ModelState.AddModelError(nameof(RegisterViewModel.Username),
                    _labels["This username is already taken. Please choose another."].Value);

            if (emailExists)
                ModelState.AddModelError(nameof(RegisterViewModel.Email),
                    _labels["This email is already registered."].Value);

            return usernameExists || emailExists;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            if (!TryGetCurrentUserId(out long userId))
                return Challenge();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound();

            var model = new ProfileViewModel
            {
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };

            await LoadAvatarAsync(userId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ProfileContent", model);
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!TryGetCurrentUserId(out long userId))
                return Challenge();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound();

            // Không tin ngày tạo tài khoản gửi từ form.
            model.CreatedAt = user.CreatedAt;

            // Nạp avatar cả khi form có lỗi.
            await LoadAvatarAsync(userId);

            if (!ModelState.IsValid)
                return View(model);

            string username = model.Username.Trim();
            string email = model.Email.Trim();

            bool usernameExists = await _context.Users
                .AnyAsync(u =>
                    u.Username == username &&
                    u.UserId != userId);

            if (usernameExists)
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "This username is already taken.");

                return View(model);
            }

            bool emailExists = await _context.Users
                .AnyAsync(u =>
                    u.Email == email &&
                    u.UserId != userId);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered.");

                return View(model);
            }

            user.Username = username;
            user.Email = email;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx &&
                      (sqlEx.Number == 2601 ||
                       sqlEx.Number == 2627))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Username or email already exists.");

                return View(model);
            }

            // Làm mới Cookie Claims sau khi đổi thông tin.
            var identity = CreateIdentity(user);

            // Giữ trạng thái Remember Me hiện tại khi làm mới cookie.
            var authentication = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = authentication.Properties
                ?? new AuthenticationProperties();

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);

            TempData["ProfileSuccess"] =
                "Profile updated successfully.";

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(IFormFile? avatar)
        {
            if (!TryGetCurrentUserId(out long userId))
                return Challenge();

            bool userExists = await _context.Users
                .AnyAsync(u => u.UserId == userId);

            if (!userExists)
                return NotFound();

            const long maxBytes = 2 * 1024 * 1024;

            if (avatar == null ||
                avatar.Length == 0 ||
                avatar.Length > maxBytes)
            {
                TempData["AvatarError"] =
                    "Please select an image smaller than 2 MB.";

                return RedirectToAction(nameof(Profile));
            }

            string extension = Path.GetExtension(
                avatar.FileName).ToLowerInvariant();

            string expectedType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => ""
            };

            if (expectedType == "" ||
                !string.Equals(
                    avatar.ContentType,
                    expectedType,
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["AvatarError"] =
                    "Only JPG and PNG images are allowed.";

                return RedirectToAction(nameof(Profile));
            }

            // Kiểm tra chữ ký đầu file.
            byte[] header = new byte[8];

            await using (var input = avatar.OpenReadStream())
            {
                int totalRead = 0;

                while (totalRead < header.Length)
                {
                    int count = await input.ReadAsync(
                        header.AsMemory(totalRead));

                    if (count == 0)
                        break;

                    totalRead += count;
                }

                bool isJpeg = totalRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF;

                bool isPng = totalRead >= 8 &&
                    header.SequenceEqual(new byte[]
                    {
                        0x89, 0x50, 0x4E, 0x47,
                        0x0D, 0x0A, 0x1A, 0x0A
                    });

                if ((expectedType == "image/jpeg" && !isJpeg) ||
                    (expectedType == "image/png" && !isPng))
                {
                    TempData["AvatarError"] =
                        "The uploaded file is not a valid image.";

                    return RedirectToAction(nameof(Profile));
                }
            }

            string webRoot = _environment.WebRootPath
                ?? Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");

            string folder = Path.Combine(
                webRoot,
                "uploads",
                "avatars");

            Directory.CreateDirectory(folder);

            string safeExtension =
                expectedType == "image/png" ? ".png" : ".jpg";

            // Không ghi đè file cũ.
            string fileName = $"{Guid.NewGuid():N}{safeExtension}";
            string physicalPath = Path.Combine(folder, fileName);
            string imagePath = $"/uploads/avatars/{fileName}";

            string? oldImagePath = null;
            bool newFileCreated = false;
            bool databaseSaved = false;

            try
            {
                // Bước 1: Lưu ảnh mới.
                await using (var output = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write))
                {
                    newFileCreated = true;
                    await avatar.CopyToAsync(output);
                }

                // Bước 2: Đọc bản ghi avatar hiện tại.
                var userImage = await _context.UserImages
                    .FirstOrDefaultAsync(x => x.UserId == userId);

                if (userImage == null)
                {
                    _context.UserImages.Add(new UserImage
                    {
                        UserId = userId,
                        ImagePath = imagePath,
                        UploadedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    oldImagePath = userImage.ImagePath;
                    userImage.ImagePath = imagePath;
                    userImage.UploadedAt = DateTime.UtcNow;
                }

                // Bước 3: Lưu đường dẫn vào SQL Server.
                await _context.SaveChangesAsync();
                databaseSaved = true;

                // Bước 4: Dọn file cũ sau khi SQL thành công.
                if (!string.IsNullOrWhiteSpace(oldImagePath) &&
                    oldImagePath.StartsWith(
                        "/uploads/avatars/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string oldFileName = Path.GetFileName(oldImagePath);

                    if (!string.IsNullOrEmpty(oldFileName) &&
                        !string.Equals(
                            oldFileName,
                            fileName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string oldPhysicalPath =
                            Path.Combine(folder, oldFileName);

                        UploadedFileCleanup.Delete(oldPhysicalPath, _logger);
                    }
                }

                TempData["ProfileSuccess"] =
                    "Profile picture updated successfully.";

                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is DbUpdateException)
            {
                _logger.LogError(ex, "Avatar update failed for user {UserId}", userId);
                if (newFileCreated && !databaseSaved)
                {
                    UploadedFileCleanup.Delete(physicalPath, _logger);
                }

                TempData["AvatarError"] =
                    "Unable to update profile picture. Please try again.";

                return RedirectToAction(nameof(Profile));
            }
        }

        [HttpGet]
        public IActionResult LogOut()
        {
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

            return Content(
                "Access denied. Your account does not have permission to access this page.");
        }

        private static ClaimsIdentity CreateIdentity(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };
            return new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        private bool TryGetCurrentUserId(out long userId)
        {
            return long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        private async Task LoadAvatarAsync(long userId)
        {
            ViewBag.AvatarPath = await _context.UserImages
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ImagePath)
                .FirstOrDefaultAsync();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            return !string.IsNullOrEmpty(returnUrl) &&
                   Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }

    }
}
