-- Baseline schema (as originally built). Safe to re-run: each table is created only if missing.
-- Constraints, indexes and later changes live in ../Scripts so existing databases get them too.

IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NULL,
    Phone        NVARCHAR(20)  NULL UNIQUE,
    Email        NVARCHAR(100) NULL,
    PasswordHash NVARCHAR(255) NULL,
    Role         NVARCHAR(20)  NULL
);
GO

IF OBJECT_ID('dbo.Riders') IS NULL
CREATE TABLE dbo.Riders (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT NULL REFERENCES dbo.Users(Id),
    IsOnline   BIT NULL DEFAULT 0,
    CurrentLat FLOAT NULL,
    CurrentLng FLOAT NULL,
    LastSeen   DATETIME NULL DEFAULT GETDATE(),
    Rating     DECIMAL(3,2) NULL DEFAULT 5.00
);
GO

IF OBJECT_ID('dbo.Orders') IS NULL
CREATE TABLE dbo.Orders (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    Reference      NVARCHAR(20)  NULL UNIQUE,
    CustomerId     INT NULL REFERENCES dbo.Users(Id),
    RiderId        INT NULL REFERENCES dbo.Riders(Id),
    Status         NVARCHAR(20)  NULL DEFAULT 'PENDING',
    PickupLat      FLOAT NULL,
    PickupLng      FLOAT NULL,
    PickupAddress  NVARCHAR(255) NULL,
    DropoffLat     FLOAT NULL,
    DropoffLng     FLOAT NULL,
    DropoffAddress NVARCHAR(255) NULL,
    Price          DECIMAL(18,2) NULL,
    DistanceKm     FLOAT NULL,
    DeliveryOtp    NVARCHAR(6)   NULL,
    CreatedAt      DATETIME NULL DEFAULT GETDATE(),
    AcceptedAt     DATETIME NULL,
    DeliveredAt    DATETIME NULL
);
GO

IF OBJECT_ID('dbo.RiderLocations') IS NULL
CREATE TABLE dbo.RiderLocations (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    RiderId   INT NULL REFERENCES dbo.Riders(Id),
    Lat       FLOAT NULL,
    Lng       FLOAT NULL,
    CreatedAt DATETIME NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.Wallets') IS NULL
CREATE TABLE dbo.Wallets (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NULL UNIQUE REFERENCES dbo.Users(Id),
    Balance   DECIMAL(18,2) NULL DEFAULT 0,
    UpdatedAt DATETIME NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.WalletTransactions') IS NULL
CREATE TABLE dbo.WalletTransactions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    WalletId    INT NULL REFERENCES dbo.Wallets(Id),
    Type        NVARCHAR(20)  NULL,
    Amount      DECIMAL(18,2) NULL,
    Reference   NVARCHAR(50)  NULL,
    Description NVARCHAR(200) NULL,
    CreatedAt   DATETIME NULL DEFAULT GETDATE()
);
GO

-- Withdrawals (Paystack transfers)
IF OBJECT_ID('dbo.Transactions') IS NULL
CREATE TABLE dbo.Transactions (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    Reference         NVARCHAR(50)  NOT NULL UNIQUE,
    UserId            INT NOT NULL,
    WalletId          INT NOT NULL CONSTRAINT FK_Transactions_Wallets REFERENCES dbo.Wallets(Id),
    Amount            DECIMAL(18,2) NOT NULL,
    Type              NVARCHAR(20)  NOT NULL,
    Status            NVARCHAR(20)  NOT NULL DEFAULT 'PENDING',
    PaystackReference NVARCHAR(100) NULL,
    IdempotencyKey    NVARCHAR(100) NOT NULL,
    CreatedAt         DATETIME2 NULL DEFAULT GETDATE(),
    UpdatedAt         DATETIME2 NULL DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.IdempotencyKeys') IS NULL
CREATE TABLE dbo.IdempotencyKeys (
    [Key]     NVARCHAR(100) NOT NULL PRIMARY KEY,
    OrderId   INT NOT NULL REFERENCES dbo.Orders(Id),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
