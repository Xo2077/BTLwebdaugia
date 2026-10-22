
-- BidHub Development Seed
-- Không thay đổi cấu trúc 9 bảng
-- Có thể chạy lại, tránh tạo trùng dữ liệu seed

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @SellerId BIGINT;
    DECLARE @ReviewerId BIGINT;

    -- Chọn tài khoản User đã đăng ký qua SignUp.
    SELECT TOP (1)
        @SellerId = UserId
    FROM dbo.Users
    WHERE Role = 'User'
    ORDER BY UserId;

    IF @SellerId IS NULL
        THROW 50001,
            'Please register one User account before seeding.',
            1;

    SELECT TOP (1)
        @ReviewerId = UserId
    FROM dbo.Users
    WHERE Role = 'Admin'
    ORDER BY UserId;

    --------------------------------------------------
    -- 1. CATEGORIES
    --------------------------------------------------

    INSERT INTO dbo.Categories (Name, Description)
    SELECT v.Name, v.Description
    FROM (VALUES
        (N'Luxury & Jewelry', N'Luxury watches and jewelry'),
        (N'Motors & Vehicles', N'Cars and motorcycles'),
        (N'Fine Art', N'Paintings and artworks'),
        (N'Premium Electronics', N'Premium electronics'),
        (N'Designer Fashion', N'Fashion and accessories'),
        (N'Collectibles', N'Rare collectible items'),
        (N'Real Estate', N'Properties and real estate'),
        (N'Home & Sports', N'Home and sports equipment')
    ) v(Name, Description)
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.Categories c
        WHERE c.Name = v.Name
    );

    --------------------------------------------------
    -- 2. PRODUCTS
    --------------------------------------------------

    -- Tất cả tên sản phẩm seed có tiền tố SEED -
    -- để phân biệt với sản phẩm thật.

    INSERT INTO dbo.Products (
        SellerId,
        CategoryId,
        Name,
        Description,
        ApprovalStatus,
        HasBeenApproved,
        ReviewedBy,
        ReviewedAt,
        CreatedAt,
        IsDeleted
    )
    SELECT
        @SellerId,
        c.CategoryId,
        v.Name,
        v.Description,
        v.ApprovalStatus,
        CASE WHEN v.ApprovalStatus = 'Approved'
             THEN 1 ELSE 0 END,
        CASE WHEN v.ApprovalStatus = 'Approved'
             THEN @ReviewerId ELSE NULL END,
        CASE WHEN v.ApprovalStatus = 'Approved'
                  AND @ReviewerId IS NOT NULL
             THEN @Now ELSE NULL END,
        @Now,
        0
    FROM (VALUES
        (N'SEED - Vintage Camera',
         N'Collectibles',
         N'Vintage film camera in good condition.',
         'Approved'),

        (N'SEED - Classic Wristwatch',
         N'Luxury & Jewelry',
         N'Classic mechanical wristwatch.',
         'Approved'),

        (N'SEED - Walnut Turntable',
         N'Premium Electronics',
         N'Vintage walnut record turntable.',
         'Approved'),

        (N'SEED - Leather Handbag',
         N'Designer Fashion',
         N'Premium genuine leather handbag.',
         'Approved'),

        (N'SEED - Antique Painting',
         N'Fine Art',
         N'Original decorative antique painting.',
         'Approved'),

        (N'SEED - Retro Motorcycle',
         N'Motors & Vehicles',
         N'Collectible retro motorcycle.',
         'Approved'),

        (N'SEED - Sports Bicycle',
         N'Home & Sports',
         N'Lightweight road bicycle.',
         'Pending'),

        (N'SEED - Luxury Apartment',
         N'Real Estate',
         N'Modern apartment listing.',
         'Pending')
    ) v(Name, CategoryName, Description, ApprovalStatus)
    JOIN dbo.Categories c
        ON c.Name = v.CategoryName
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.Products p
        WHERE p.Name = v.Name
    );

    --------------------------------------------------
    -- 3. AUCTIONS
    --------------------------------------------------

    INSERT INTO dbo.Auctions (
        ProductId,
        StartingPrice,
        BidStep,
        EndTimeMode,
        DurationMinutes,
        StartAt,
        OriginalEndAt,
        CurrentEndAt,
        ExtendedOnce,
        ApprovalStatus,
        ReviewedBy,
        ReviewedAt,
        CreatedAt
    )
    SELECT
        p.ProductId,
        v.StartingPrice,
        v.BidStep,
        'Duration',
        v.DurationMinutes,

        DATEADD(MINUTE, v.StartOffset, @Now),

        DATEADD(
            MINUTE,
            v.StartOffset + v.DurationMinutes,
            @Now
        ),

        DATEADD(
            MINUTE,
            v.StartOffset + v.DurationMinutes,
            @Now
        ),

        0,
        v.ApprovalStatus,

        CASE WHEN v.ApprovalStatus = 'Approved'
             THEN @ReviewerId ELSE NULL END,

        CASE WHEN v.ApprovalStatus = 'Approved'
                  AND @ReviewerId IS NOT NULL
             THEN @Now ELSE NULL END,

        @Now

    FROM (VALUES
        -- Four live auctions
        (N'SEED - Vintage Camera',
         125, 5, -60, 4320, 'Approved'),

        (N'SEED - Classic Wristwatch',
         210, 10, -30, 2880, 'Approved'),

        (N'SEED - Walnut Turntable',
         295, 10, -60, 5760, 'Approved'),

        (N'SEED - Leather Handbag',
         465, 20, -120, 7200, 'Approved'),

        -- Two upcoming auctions
        (N'SEED - Antique Painting',
         800, 25, 1440, 4320, 'Approved'),

        (N'SEED - Retro Motorcycle',
         5000, 100, 2880, 10080, 'Approved'),

        -- Two pending auctions
        (N'SEED - Sports Bicycle',
         350, 10, 1440, 4320, 'Pending'),

        (N'SEED - Luxury Apartment',
         50000, 500, 1440, 10080, 'Pending')
    ) v(
        ProductName,
        StartingPrice,
        BidStep,
        StartOffset,
        DurationMinutes,
        ApprovalStatus
    )
    JOIN dbo.Products p
        ON p.Name = v.ProductName
       AND p.SellerId = @SellerId
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.Auctions a
        WHERE a.ProductId = p.ProductId
    );

    COMMIT TRANSACTION;

    PRINT 'BidHub seed completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

--------------------------------------------------
-- 4. VERIFY
--------------------------------------------------

SELECT 'Categories' AS TableName, COUNT(*) AS Total
FROM dbo.Categories
UNION ALL
SELECT 'Products', COUNT(*) FROM dbo.Products
UNION ALL
SELECT 'Auctions', COUNT(*) FROM dbo.Auctions;

SELECT
    p.ProductId,
    p.Name,
    p.ApprovalStatus AS ProductStatus,
    a.AuctionId,
    a.StartingPrice,
    a.ApprovalStatus AS AuctionStatus,
    a.StartAt,
    a.CurrentEndAt
FROM dbo.Products p
JOIN dbo.Auctions a
    ON a.ProductId = p.ProductId
WHERE p.Name LIKE N'SEED - %'
ORDER BY a.AuctionId;





SELECT
    a.AuctionId,
    p.Name AS ProductName,
    a.CurrentEndAt,
    a.Result,
    a.WinnerBidId,
    COUNT(b.BidId) AS TotalBids,
    MAX(b.Amount) AS HighestBid,
    o.OrderId,
    o.Status AS OrderStatus
FROM dbo.Auctions a
JOIN dbo.Products p
    ON p.ProductId = a.ProductId
LEFT JOIN dbo.Bids b
    ON b.AuctionId = a.AuctionId
LEFT JOIN dbo.Orders o
    ON o.AuctionId = a.AuctionId
GROUP BY
    a.AuctionId,
    p.Name,
    a.CurrentEndAt,
    a.Result,
    a.WinnerBidId,
    o.OrderId,
    o.Status
ORDER BY a.AuctionId DESC;


 
CREATE TABLE dbo.UserImages
(
    ImageId BIGINT IDENTITY(1,1) NOT NULL,
    UserId BIGINT NOT NULL,
    ImagePath NVARCHAR(500) NOT NULL,
    UploadedAt DATETIME2(3) NOT NULL
        CONSTRAINT DF_UserImages_UploadedAt
        DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_UserImages
        PRIMARY KEY (ImageId),

    CONSTRAINT UQ_UserImages_UserId
        UNIQUE (UserId),

    CONSTRAINT FK_UserImages_Users
        FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId),

    CONSTRAINT CK_UserImages_ImagePath
        CHECK (LEN(TRIM(ImagePath)) > 0)
);


SELECT
    a.AuctionId,
    p.Name,
    p.ApprovalStatus AS ProductStatus,
    a.ApprovalStatus AS AuctionStatus,
    a.RejectionReason,
    a.ReviewedAt,
    a.StartAt,
    a.CurrentEndAt,
    a.Result
FROM dbo.Auctions a
JOIN dbo.Products p ON p.ProductId = a.ProductId
ORDER BY a.AuctionId DESC;