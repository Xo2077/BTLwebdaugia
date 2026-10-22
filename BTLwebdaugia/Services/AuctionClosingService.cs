using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BTLwebdaugia.Services
{
    public sealed class AuctionClosingService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuctionClosingService> _logger;

        public AuctionClosingService(
            IServiceScopeFactory scopeFactory,
            ILogger<AuctionClosingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            // Chạy ngay khi ứng dụng khởi động,
            // sau đó kiểm tra định kỳ mỗi 60 giây.
            await CloseExpiredAuctionsAsync(stoppingToken);

            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(60));

            try
            {
                while (await timer.WaitForNextTickAsync(
                    stoppingToken))
                {
                    await CloseExpiredAuctionsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Ứng dụng đang dừng bình thường.
            }
        }

        private async Task CloseExpiredAuctionsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<BidHubDbContext>();

                DateTime now = DateTime.UtcNow;

                // Chọn các phiên có khả năng đã kết thúc.
                // Sẽ kiểm tra lại sau khi khóa từng phiên.
                var auctionIds = await db.Auctions
                    .AsNoTracking()
                    .Where(a =>
                        a.ApprovalStatus == "Approved" &&
                        a.Product.ApprovalStatus == "Approved" &&
                        !a.Product.IsDeleted &&
                        a.Result == null &&
                        a.CurrentEndAt <= now)
                    .OrderBy(a => a.CurrentEndAt)
                    .Select(a => a.AuctionId)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                foreach (long auctionId in auctionIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await CloseOneAuctionAsync(
                            auctionId,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Một phiên lỗi không chặn phiên khác.
                        _logger.LogError(
                            ex,
                            "Failed to close auction {AuctionId}",
                            auctionId);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Ứng dụng đang dừng.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to scan expired auctions.");
            }
        }

        private async Task CloseOneAuctionAsync(
            long auctionId,
            CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<BidHubDbContext>();

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Dùng cùng kiểu khóa với PlaceBid().
            // Chỉ một transaction được xử lý Auction này.
            var auction = await db.Auctions
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Auctions
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE AuctionId = {auctionId}")
                .Include(a => a.Product)
                .SingleOrDefaultAsync(cancellationToken);

            if (auction == null)
                return;

            DateTime now = DateTime.UtcNow;

            // Kiểm tra lại sau khi đã lấy khóa.
            // Không dùng kết quả kiểm tra từ lần quét ban đầu.
            if (auction.Result != null ||
                auction.ApprovalStatus != "Approved" ||
                auction.Product.ApprovalStatus != "Approved" ||
                auction.Product.IsDeleted ||
                auction.CurrentEndAt > now)
            {
                return;
            }

            // Xếp hạng Bid:
            // 1. Giá cao nhất
            // 2. Thời gian sớm nhất
            // 3. BidId nhỏ nhất
            var winningBid = await db.Bids
                .AsNoTracking()
                .Where(b => b.AuctionId == auctionId)
                .OrderByDescending(b => b.Amount)
                .ThenBy(b => b.PlacedAt)
                .ThenBy(b => b.BidId)
                .FirstOrDefaultAsync(cancellationToken);

            // Không có Bid: Unsold, không tạo Order.
            if (winningBid == null)
            {
                auction.Result = "Unsold";
                auction.WinnerBidId = null;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Auction {AuctionId} closed as Unsold.",
                    auctionId);

                return;
            }

            // Kiểm tra tình trạng Order trước khi tạo.
            // Nếu đã tồn tại Order mà Auction chưa được chốt,
            // đây là dữ liệu bất thường, không tự sửa.
            bool orderExists = await db.Orders
                .AnyAsync(
                    o => o.AuctionId == auctionId,
                    cancellationToken);

            if (orderExists)
            {
                throw new InvalidOperationException(
                    $"Auction {auctionId} already has an Order " +
                    "but is not finalized.");
            }

            // Lưu Bid thắng đúng cùng phiên đấu giá.
            auction.Result = "Sold";
            auction.WinnerBidId = winningBid.BidId;

            var order = new Order
            {
                AuctionId = auction.AuctionId,
                WinnerId = winningBid.BidderId,
                FinalAmount = winningBid.Amount,
                Status = "PendingPayment",
                PaidAt = null,
                ShippedAt = null,
                CompletedAt = null
            };

            db.Orders.Add(order);

            // Hai thay đổi nằm trong cùng transaction:
            // Auction: Sold + WinnerBidId
            // Orders: PendingPayment
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Auction {AuctionId} sold. " +
                "Winner: {WinnerId}, Amount: {Amount}, " +
                "OrderId: {OrderId}",
                auctionId,
                winningBid.BidderId,
                winningBid.Amount,
                order.OrderId);
        }
    }
}
