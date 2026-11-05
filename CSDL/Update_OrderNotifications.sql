-- Extend notifications without changing existing business data.
USE CSDLWEBDAUGIA;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
CREATE TABLE dbo.Notifications (
    NotificationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    AuctionId BIGINT NULL,
    EventType VARCHAR(20) NOT NULL,
    ProductName NVARCHAR(200) NOT NULL,
    Reason NVARCHAR(1000) NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),
    ReadAt DATETIME2(3) NULL,
    CONSTRAINT FK_Notifications_User FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Notifications_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT FK_Notifications_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auctions(AuctionId),
    CONSTRAINT CK_Notifications_EventType CHECK (EventType IN ('ProductApproved','ProductRejected','AuctionApproved','AuctionRejected','AuctionWon','AuctionSold','PaymentSent','PaymentReceived','OrderShipped','OrderCompleted'))
);
CREATE INDEX IX_Notifications_UserTime ON dbo.Notifications (UserId, CreatedAt DESC, NotificationId DESC);
CREATE INDEX IX_Notifications_Unread ON dbo.Notifications (UserId) WHERE ReadAt IS NULL;
END
ELSE
BEGIN
    IF OBJECT_ID(N'dbo.CK_Notifications_EventType', N'C') IS NOT NULL
        ALTER TABLE dbo.Notifications DROP CONSTRAINT CK_Notifications_EventType;
    ALTER TABLE dbo.Notifications WITH CHECK ADD CONSTRAINT CK_Notifications_EventType
        CHECK (EventType IN ('ProductApproved','ProductRejected','AuctionApproved','AuctionRejected','AuctionWon','AuctionSold','PaymentSent','PaymentReceived','OrderShipped','OrderCompleted'));
END;

COMMIT TRANSACTION;
GO
