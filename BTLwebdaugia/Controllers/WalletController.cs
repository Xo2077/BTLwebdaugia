
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class WalletController : Controller
    {
        private readonly BidHubDbContext _context;

        private const decimal MaxBalance =
            999999999999999999m;

        public WalletController(BidHubDbContext context)
        {
            _context = context;
        }

        private bool TryGetUserId(out long userId)
        {
            return long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out long userId))
                return Challenge();

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.Username,
                    u.CreatedAt,
                    u.Role
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            if (user.Role != "User")
                return Forbid();

            var wallet = await _context.Wallets
                .AsNoTracking()
                .Where(w => w.UserId == userId)
                .Select(w => new
                {
                    w.WalletId,
                    w.Balance
                })
                .FirstOrDefaultAsync();

            var model = new WalletViewModel
            {
                Username = user.Username,
                MemberSince = user.CreatedAt,
                Balance = wallet?.Balance ?? 0m
            };

            if (wallet != null)
            {
                model.Transactions =
                    await _context.WalletTransactions
                        .AsNoTracking()
                        .Where(t => t.WalletId == wallet.WalletId)
                        .OrderByDescending(t => t.CreatedAt)
                        .ThenByDescending(t => t.TransactionId)
                        .Take(50)
                        .Select(t => new WalletTransactionItemViewModel
                        {
                            TransactionId = t.TransactionId,
                            TransactionType = t.TransactionType,
                            Description =
                                t.TransactionType == "TopUp"
                                    ? "Virtual wallet top-up"
                                    : t.TransactionType == "PaymentDebit"
                                        ? "Auction payment"
                                        : "Payment received",
                            Amount = t.Amount,
                            CreatedAt = t.CreatedAt
                        })
                        .ToListAsync();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUp(
            TopUpViewModel model,
            Guid requestId)
        {
            if (!TryGetUserId(out long userId))
                return Challenge();

            // Ví lưu DECIMAL(18,0): chỉ nhận USD nguyên.
            // Giới hạn nạp mỗi lần ở mức hợp lý cho demo.
            if (!ModelState.IsValid ||
                model.Amount < 1m ||
                model.Amount > 1000000m ||
                model.Amount != decimal.Truncate(model.Amount) ||
                requestId == Guid.Empty)
            {
                TempData["WalletError"] =
                    "Enter a whole USD amount from $1 to $1,000,000.";

                return RedirectToAction(nameof(Index));
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            // Khóa theo User để tránh hai request cùng tạo
            // ví hoặc cùng cập nhật số dư của một tài khoản.
            var user = await _context.Users
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Users
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE UserId = {userId}")
                .SingleOrDefaultAsync();

            if (user == null)
                return NotFound();

            if (user.Role != "User")
                return Forbid();

            // Nếu cùng RequestId đã được xử lý,
            // không cộng tiền lần thứ hai.
            bool alreadyProcessed =
                await _context.WalletTransactions
                    .AnyAsync(t =>
                        t.RequestId == requestId &&
                        t.TransactionType == "TopUp");

            if (alreadyProcessed)
            {
                TempData["WalletSuccess"] =
                    "This top-up request was already processed.";

                return RedirectToAction(nameof(Index));
            }

            var wallet = await _context.Wallets
                .SingleOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = 0m,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Wallets.Add(wallet);

                // Tạo ví để lấy WalletId.
                await _context.SaveChangesAsync();
            }

            if (wallet.Balance > MaxBalance - model.Amount)
            {
                TempData["WalletError"] =
                    "Wallet balance limit exceeded.";

                return RedirectToAction(nameof(Index));
            }

            wallet.Balance += model.Amount;

            _context.WalletTransactions.Add(
                new WalletTransaction
                {
                    WalletId = wallet.WalletId,
                    OrderId = null,
                    TransactionType = "TopUp",
                    Amount = model.Amount,
                    CreatedAt = DateTime.UtcNow,
                    RequestId = requestId,
                    TransferGroupId = null
                });

            // Số dư và lịch sử nạp được lưu
            // trong cùng transaction.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["WalletSuccess"] =
                $"Added ${model.Amount:N0} to your virtual wallet.";

            return RedirectToAction(nameof(Index));
        }
    }
}
