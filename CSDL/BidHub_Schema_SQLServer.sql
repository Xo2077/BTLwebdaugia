/*
 BidHub - SQL Server database schema v1.0
 ERD approved 2026-10-20. Nine core tables.
 All date/time fields use UTC by convention.
 Currency: whole USD (DECIMAL(18,0)).
 Run against an EMPTY chosen database. This script does not CREATE DATABASE.
 IMPORTANT: cross-table rules and multi-step actions must be enforced in application
 transactions or purpose-built stored procedures; see notes at end.
*/
CREATE DATABASE CSDLWEBDAUGIA;
GO
USE CSDLWEBDAUGIA;
GO
SET XACT_ABORT ON;
GO

CREATE TABLE dbo.Users (
    UserId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username NVARCHAR(50) COLLATE Latin1_General_100_CI_AS NOT NULL,
    Email NVARCHAR(254) COLLATE Latin1_General_100_CI_AS NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    [Role] VARCHAR(10) NOT NULL CONSTRAINT DF_Users_Role DEFAULT ('User'),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role CHECK ([Role] IN ('User','Admin')),
    CONSTRAINT CK_Users_Username_NotEmpty CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
    CONSTRAINT CK_Users_Email_NotEmpty CHECK (LEN(LTRIM(RTRIM(Email))) > 0)
);
GO

CREATE TABLE dbo.Categories (
    CategoryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    [Name] NVARCHAR(120) NOT NULL,
    [Description] NVARCHAR(1000) NULL,
    CONSTRAINT UQ_Categories_Name UNIQUE ([Name]),
    CONSTRAINT CK_Categories_Name_NotEmpty CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO

CREATE TABLE dbo.Products (
    ProductId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    SellerId BIGINT NOT NULL,
    CategoryId INT NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    ApprovalStatus VARCHAR(10) NOT NULL CONSTRAINT DF_Products_ApprovalStatus DEFAULT ('Pending'),
    RejectionReason NVARCHAR(1000) NULL,
    ReviewedBy BIGINT NULL,
    ReviewedAt DATETIME2(3) NULL,
    HasBeenApproved BIT NOT NULL CONSTRAINT DF_Products_HasBeenApproved DEFAULT (0),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    IsDeleted BIT NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT (0),
    DeletedAt DATETIME2(3) NULL,
    DeletedBy BIGINT NULL,
    CONSTRAINT FK_Products_Seller FOREIGN KEY (SellerId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
    CONSTRAINT FK_Products_Reviewer FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Products_DeletedBy FOREIGN KEY (DeletedBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Products_Approval CHECK (ApprovalStatus IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_Products_ReviewFields CHECK (
        (ApprovalStatus = 'Pending' AND ReviewedBy IS NULL AND ReviewedAt IS NULL AND RejectionReason IS NULL)
        OR (ApprovalStatus = 'Approved' AND ReviewedBy IS NOT NULL AND ReviewedAt IS NOT NULL AND RejectionReason IS NULL AND HasBeenApproved = 1)
        OR (ApprovalStatus = 'Rejected' AND ReviewedBy IS NOT NULL AND ReviewedAt IS NOT NULL AND RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(RejectionReason))) > 0)
    ),
    CONSTRAINT CK_Products_DeleteFields CHECK (
        (IsDeleted = 0 AND DeletedAt IS NULL AND DeletedBy IS NULL)
        OR (IsDeleted = 1 AND DeletedAt IS NOT NULL AND HasBeenApproved = 0)
    ),
    CONSTRAINT CK_Products_Name_NotEmpty CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO

CREATE TABLE dbo.ProductImages (
    ImageId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductImages PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    ImagePath NVARCHAR(1000) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_ProductImages_SortOrder DEFAULT (0),
    CONSTRAINT FK_ProductImages_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT CK_ProductImages_SortOrder CHECK (SortOrder >= 0),
    CONSTRAINT CK_ProductImages_Path CHECK (LEN(LTRIM(RTRIM(ImagePath))) > 0),
    CONSTRAINT UQ_ProductImages_ProductSort UNIQUE (ProductId, SortOrder)
);
GO

CREATE TABLE dbo.Auctions (
    AuctionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Auctions PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    StartingPrice DECIMAL(18,0) NOT NULL,
    BidStep DECIMAL(18,0) NOT NULL,
    EndTimeMode VARCHAR(16) NOT NULL,
    DurationMinutes INT NULL,
    StartAt DATETIME2(3) NOT NULL,
    OriginalEndAt DATETIME2(3) NOT NULL,
    CurrentEndAt DATETIME2(3) NOT NULL,
    ExtendedOnce BIT NOT NULL CONSTRAINT DF_Auctions_ExtendedOnce DEFAULT (0),
    ApprovalStatus VARCHAR(10) NOT NULL CONSTRAINT DF_Auctions_ApprovalStatus DEFAULT ('Pending'),
    RejectionReason NVARCHAR(1000) NULL,
    ReviewedBy BIGINT NULL,
    ReviewedAt DATETIME2(3) NULL,
    Result VARCHAR(6) NULL,
    WinnerBidId BIGINT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Auctions_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Auctions_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT FK_Auctions_Reviewer FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Auctions_Price CHECK (StartingPrice >= 50 AND BidStep > 0),
    CONSTRAINT CK_Auctions_EndMode CHECK (
       (EndTimeMode = 'Duration' AND DurationMinutes BETWEEN 60 AND 43200)
       OR (EndTimeMode = 'FixedEndTime' AND DurationMinutes IS NULL)
    ),
    CONSTRAINT CK_Auctions_TimeOrder CHECK (StartAt < OriginalEndAt AND OriginalEndAt <= CurrentEndAt),
    CONSTRAINT CK_Auctions_InitialDuration CHECK (DATEDIFF_BIG(SECOND, StartAt, OriginalEndAt) BETWEEN 3600 AND 2592000),
    CONSTRAINT CK_Auctions_DurationMatches CHECK (
       EndTimeMode <> 'Duration' OR DATEDIFF_BIG(SECOND, StartAt, OriginalEndAt) = CONVERT(BIGINT, DurationMinutes) * 60
    ),
    CONSTRAINT CK_Auctions_ExtensionFlag CHECK (ExtendedOnce = 1 OR CurrentEndAt = OriginalEndAt),
    CONSTRAINT CK_Auctions_Approval CHECK (ApprovalStatus IN ('Pending','Approved','Rejected')),
    CONSTRAINT CK_Auctions_ReviewFields CHECK (
      (ApprovalStatus = 'Pending' AND ReviewedBy IS NULL AND ReviewedAt IS NULL AND RejectionReason IS NULL)
      OR (ApprovalStatus = 'Approved' AND ReviewedBy IS NOT NULL AND ReviewedAt IS NOT NULL AND RejectionReason IS NULL)
      OR (ApprovalStatus = 'Rejected' AND ReviewedBy IS NOT NULL AND ReviewedAt IS NOT NULL AND RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(RejectionReason))) > 0)
    ),
    CONSTRAINT CK_Auctions_Result CHECK (Result IS NULL OR Result IN ('Sold','Unsold')),
    CONSTRAINT CK_Auctions_ResultWinner CHECK (
      (Result = 'Sold' AND WinnerBidId IS NOT NULL)
      OR (Result = 'Unsold' AND WinnerBidId IS NULL)
      OR (Result IS NULL AND WinnerBidId IS NULL)
    ),
    CONSTRAINT UQ_Auctions_IdWinnerComposite UNIQUE (AuctionId, WinnerBidId)
);
GO

CREATE TABLE dbo.Bids (
    BidId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bids PRIMARY KEY,
    AuctionId BIGINT NOT NULL,
    BidderId BIGINT NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    PlacedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Bids_PlacedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Bids_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auctions(AuctionId),
    CONSTRAINT FK_Bids_Bidder FOREIGN KEY (BidderId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Bids_Amount CHECK (Amount > 0),
    CONSTRAINT UQ_Bids_AuctionBidComposite UNIQUE (AuctionId, BidId)
);
GO

/* Composite FK ensures winning Bid belongs to this Auction. */
ALTER TABLE dbo.Auctions ADD CONSTRAINT FK_Auctions_WinnerBidSameAuction
    FOREIGN KEY (AuctionId, WinnerBidId) REFERENCES dbo.Bids(AuctionId, BidId);
GO

CREATE TABLE dbo.Wallets (
    WalletId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Wallets PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Balance DECIMAL(18,0) NOT NULL CONSTRAINT DF_Wallets_Balance DEFAULT (0),
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Wallets_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Wallets_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_Wallets_User UNIQUE (UserId),
    CONSTRAINT CK_Wallets_Balance CHECK (Balance >= 0)
);
GO

CREATE TABLE dbo.Orders (
    OrderId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    AuctionId BIGINT NOT NULL,
    WinnerId BIGINT NOT NULL,
    FinalAmount DECIMAL(18,0) NOT NULL,
    [Status] VARCHAR(16) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT ('PendingPayment'),
    PaidAt DATETIME2(3) NULL,
    ShippedAt DATETIME2(3) NULL,
    CompletedAt DATETIME2(3) NULL,
    CONSTRAINT FK_Orders_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auctions(AuctionId),
    CONSTRAINT FK_Orders_Winner FOREIGN KEY (WinnerId) REFERENCES dbo.Users(UserId),
    CONSTRAINT UQ_Orders_Auction UNIQUE (AuctionId),
    CONSTRAINT CK_Orders_FinalAmount CHECK (FinalAmount > 0),
    CONSTRAINT CK_Orders_Status CHECK ([Status] IN ('PendingPayment','Paid','Shipping','Completed')),
    CONSTRAINT CK_Orders_ProgressionFields CHECK (
        ([Status] = 'PendingPayment' AND PaidAt IS NULL AND ShippedAt IS NULL AND CompletedAt IS NULL)
        OR ([Status] = 'Paid' AND PaidAt IS NOT NULL AND ShippedAt IS NULL AND CompletedAt IS NULL)
        OR ([Status] = 'Shipping' AND PaidAt IS NOT NULL AND ShippedAt IS NOT NULL AND CompletedAt IS NULL)
        OR ([Status] = 'Completed' AND PaidAt IS NOT NULL AND ShippedAt IS NOT NULL AND CompletedAt IS NOT NULL)
    ),
    CONSTRAINT CK_Orders_TimeProgression CHECK (
        (ShippedAt IS NULL OR ShippedAt >= PaidAt)
        AND (CompletedAt IS NULL OR CompletedAt >= ShippedAt)
    )
);
GO

CREATE TABLE dbo.WalletTransactions (
    TransactionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalletTransactions PRIMARY KEY,
    WalletId BIGINT NOT NULL,
    OrderId BIGINT NULL,
    TransactionType VARCHAR(16) NOT NULL,
    Amount DECIMAL(18,0) NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_WalletTransactions_CreatedAt DEFAULT SYSUTCDATETIME(),
    TransferGroupId UNIQUEIDENTIFIER NULL,
    RequestId UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_WalletTransactions_Wallet FOREIGN KEY (WalletId) REFERENCES dbo.Wallets(WalletId),
    CONSTRAINT FK_WalletTransactions_Order FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId),
    CONSTRAINT CK_WalletTransactions_Amount CHECK (Amount > 0),
    CONSTRAINT CK_WalletTransactions_Type CHECK (TransactionType IN ('TopUp','PaymentDebit','PaymentCredit')),
    CONSTRAINT CK_WalletTransactions_Fields CHECK (
       (TransactionType = 'TopUp' AND OrderId IS NULL AND RequestId IS NOT NULL AND TransferGroupId IS NULL)
       OR (TransactionType IN ('PaymentDebit','PaymentCredit') AND OrderId IS NOT NULL AND RequestId IS NULL AND TransferGroupId IS NOT NULL)
    )
);
GO

CREATE UNIQUE INDEX UX_WalletTransactions_TopUp_Request
    ON dbo.WalletTransactions (RequestId)
    WHERE TransactionType = 'TopUp' AND RequestId IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_WalletTransactions_Payment_OrderType
    ON dbo.WalletTransactions (OrderId, TransactionType)
    WHERE OrderId IS NOT NULL AND TransactionType IN ('PaymentDebit','PaymentCredit');
GO
CREATE INDEX IX_Products_Seller ON dbo.Products (SellerId);
CREATE INDEX IX_Products_Category ON dbo.Products (CategoryId);
CREATE INDEX IX_ProductImages_Product ON dbo.ProductImages (ProductId);
CREATE INDEX IX_Auctions_Product ON dbo.Auctions (ProductId);
CREATE INDEX IX_Auctions_PublicBrowse ON dbo.Auctions (ApprovalStatus, StartAt, CurrentEndAt);
CREATE INDEX IX_Bids_AuctionRank ON dbo.Bids (AuctionId, Amount DESC, PlacedAt ASC, BidId ASC);
CREATE INDEX IX_Bids_Bidder ON dbo.Bids (BidderId);
CREATE INDEX IX_WalletTransactions_WalletTime ON dbo.WalletTransactions (WalletId, CreatedAt DESC);
GO

/*
REQUIRED AT APPLICATION/SERVICE TRANSACTION LAYER (not expressible in these table-only constraints):
 1. Registration: Role User -> create exactly one Wallet with Balance 0 atomically.
    Admin -> do not create Wallet. Restrict Admin creation and role changes.
 2. Username remains immutable; HasBeenApproved remains true once set.
    Product soft-delete only before first approval; approved product fields immutable.
 3. Admin product approval before auction approval. Only seller(User) owns product;
    bidder(User) cannot bid own item or consecutively outbid self.
 4. One active/pending auction per product; relist only when latest is Unsold.
 5. Validate start/end scheduling and late approval; extend once upon a valid bid
    within last five minutes, then CurrentEndAt = bid acceptance UTC + 5 min.
 6. Serialize bid acceptance and auction close, enforce increment and UTC clock;
    atomically finalize Result, WinnerBidId, and exactly one Order for Sold.
    Ensure Orders.WinnerId/FinalAmount matches selected winning Bid and seller exists.
 7. TopUp: validate RequestId reuse, atomically update Balance and write ledger.
 8. Payment: serialize by Order and both Wallets; atomically debit winner, credit
    seller, insert one matching debit/credit pair and set Order Paid. On failure rollback.
    Ledger pair must use matching OrderId/TransferGroupId and correct wallets/amounts.
 9. Paid -> Shipping -> Completed transitions and times are simulation only.
10. All timestamps are UTC; display in Asia/Ho_Chi_Minh (UTC+07:00).
11. Text lengths, normalization and stricter input-validation remain backend tasks.
*/
