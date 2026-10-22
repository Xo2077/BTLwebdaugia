using System;
using System.Collections.Generic;
using BTLwebdaugia.Models;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Data;

public partial class BidHubDbContext : DbContext
{
    public BidHubDbContext(DbContextOptions<BidHubDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Auction> Auctions { get; set; }

    public virtual DbSet<Bid> Bids { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserImage> UserImages { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auction>(entity =>
        {
            entity.HasIndex(e => e.ProductId, "IX_Auctions_Product");

            entity.HasIndex(e => new { e.ApprovalStatus, e.StartAt, e.CurrentEndAt }, "IX_Auctions_PublicBrowse");

            entity.HasIndex(e => new { e.AuctionId, e.WinnerBidId }, "UQ_Auctions_IdWinnerComposite").IsUnique();

            entity.Property(e => e.AuctionId).ValueGeneratedOnAdd();
            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_Auctions_ApprovalStatus");
            entity.Property(e => e.BidStep).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Auctions_CreatedAt");
            entity.Property(e => e.CurrentEndAt).HasPrecision(3);
            entity.Property(e => e.EndTimeMode)
                .HasMaxLength(16)
                .IsUnicode(false);
            entity.Property(e => e.OriginalEndAt).HasPrecision(3);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.Result)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.ReviewedAt).HasPrecision(3);
            entity.Property(e => e.StartAt).HasPrecision(3);
            entity.Property(e => e.StartingPrice).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.Product).WithMany(p => p.Auctions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Auctions_Product");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.Auctions)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_Auctions_Reviewer");

            entity.HasOne(d => d.Bid).WithOne(p => p.Auction)
                .HasPrincipalKey<Bid>(p => new { p.AuctionId, p.BidId })
                .HasForeignKey<Auction>(d => new { d.AuctionId, d.WinnerBidId })
                .HasConstraintName("FK_Auctions_WinnerBidSameAuction");
        });

        modelBuilder.Entity<Bid>(entity =>
        {
            entity.HasIndex(e => new { e.AuctionId, e.Amount, e.PlacedAt, e.BidId }, "IX_Bids_AuctionRank").IsDescending(false, true, false, false);

            entity.HasIndex(e => e.BidderId, "IX_Bids_Bidder");

            entity.HasIndex(e => new { e.AuctionId, e.BidId }, "UQ_Bids_AuctionBidComposite").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.PlacedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Bids_PlacedAt");

            entity.HasOne(d => d.AuctionNavigation).WithMany(p => p.Bids)
                .HasForeignKey(d => d.AuctionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bids_Auction");

            entity.HasOne(d => d.Bidder).WithMany(p => p.Bids)
                .HasForeignKey(d => d.BidderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bids_Bidder");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Categories_Name").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.AuctionId, "UQ_Orders_Auction").IsUnique();

            entity.Property(e => e.CompletedAt).HasPrecision(3);
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.PaidAt).HasPrecision(3);
            entity.Property(e => e.ShippedAt).HasPrecision(3);
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .IsUnicode(false)
                .HasDefaultValue("PendingPayment", "DF_Orders_Status");

            entity.HasOne(d => d.Auction).WithOne(p => p.Order)
                .HasForeignKey<Order>(d => d.AuctionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Auction");

            entity.HasOne(d => d.Winner).WithMany(p => p.Orders)
                .HasForeignKey(d => d.WinnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Winner");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.CategoryId, "IX_Products_Category");

            entity.HasIndex(e => e.SellerId, "IX_Products_Seller");

            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_Products_ApprovalStatus");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Products_CreatedAt");
            entity.Property(e => e.DeletedAt).HasPrecision(3);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.ReviewedAt).HasPrecision(3);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Category");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.ProductDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("FK_Products_DeletedBy");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ProductReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_Products_Reviewer");

            entity.HasOne(d => d.Seller).WithMany(p => p.ProductSellers)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Seller");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.ImageId);

            entity.HasIndex(e => e.ProductId, "IX_ProductImages_Product");

            entity.HasIndex(e => new { e.ProductId, e.SortOrder }, "UQ_ProductImages_ProductSort").IsUnique();

            entity.Property(e => e.ImagePath).HasMaxLength(1000);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImages_Product");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.Username, "UQ_Users_Username").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Users_CreatedAt");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .UseCollation("Latin1_General_100_CI_AS");
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.Role)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("User", "DF_Users_Role");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .UseCollation("Latin1_General_100_CI_AS");
        });


        modelBuilder.Entity<UserImage>(entity =>
        {
            entity.HasKey(e => e.ImageId);

            entity.ToTable("UserImages");

            entity.HasIndex(e => e.UserId)
                .IsUnique();

            entity.Property(e => e.ImagePath)
                .HasMaxLength(500);

            entity.Property(e => e.UploadedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UserImages_Users");
        });


        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UQ_Wallets_User").IsUnique();

            entity.Property(e => e.Balance).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_Wallets_CreatedAt");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wallets_User");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId);

            entity.HasIndex(e => new { e.WalletId, e.CreatedAt }, "IX_WalletTransactions_WalletTime").IsDescending(false, true);

            entity.HasIndex(e => new { e.OrderId, e.TransactionType }, "UX_WalletTransactions_Payment_OrderType")
                .IsUnique()
                .HasFilter("([OrderId] IS NOT NULL AND ([TransactionType] IN ('PaymentDebit', 'PaymentCredit')))");

            entity.HasIndex(e => e.RequestId, "UX_WalletTransactions_TopUp_Request")
                .IsUnique()
                .HasFilter("([TransactionType]='TopUp' AND [RequestId] IS NOT NULL)");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_WalletTransactions_CreatedAt");
            entity.Property(e => e.TransactionType)
                .HasMaxLength(16)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_WalletTransactions_Order");

            entity.HasOne(d => d.Wallet).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WalletTransactions_Wallet");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
