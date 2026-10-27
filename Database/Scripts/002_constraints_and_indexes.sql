-- Integrity rules the database itself enforces, so a buggy caller can't bypass them.
-- Idempotent: every change checks whether it has already been applied.

-- One account per email (Phone is already UNIQUE). Also closes the register check-then-insert race.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_Email')
    CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_Role')
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Role CHECK (Role IN ('CUSTOMER', 'RIDER', 'ADMIN'));
GO

-- One rider profile per user
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Riders_UserId')
    CREATE UNIQUE INDEX UX_Riders_UserId ON dbo.Riders(UserId) WHERE UserId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Orders_Status')
    ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Orders_Status CHECK (Status IN ('PENDING', 'ACCEPTED', 'DELIVERED', 'CANCELLED'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Orders_Price')
    ALTER TABLE dbo.Orders ADD CONSTRAINT CK_Orders_Price CHECK (Price >= 0);
GO

-- Concurrency backstop: a rider can hold at most one active (ACCEPTED) order.
-- Even if two accept requests race past every check in the procedure, the second insert into
-- this index fails, so a rider can never be double-booked.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Orders_ActiveRider')
    CREATE UNIQUE INDEX UX_Orders_ActiveRider ON dbo.Orders(RiderId) WHERE Status = 'ACCEPTED';
GO

-- Supports the pending-orders list and the stale-order background job
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_Status_CreatedAt')
    CREATE INDEX IX_Orders_Status_CreatedAt ON dbo.Orders(Status, CreatedAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_CustomerId_CreatedAt')
    CREATE INDEX IX_Orders_CustomerId_CreatedAt ON dbo.Orders(CustomerId, CreatedAt DESC);
GO

-- Money can never go negative, whatever the application does
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Wallets_Balance')
    ALTER TABLE dbo.Wallets ADD CONSTRAINT CK_Wallets_Balance CHECK (Balance >= 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Transactions_Amount')
    ALTER TABLE dbo.Transactions ADD CONSTRAINT CK_Transactions_Amount CHECK (Amount > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Transactions_Status')
    ALTER TABLE dbo.Transactions ADD CONSTRAINT CK_Transactions_Status CHECK (Status IN ('PENDING', 'SUCCESS', 'FAILED'));
GO

-- Idempotency keys are scoped per user: two users picking the same key must not collide
-- (or see each other's transaction).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Idempotency')
    DROP INDEX IX_Transactions_Idempotency ON dbo.Transactions;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Transactions_User_Idempotency')
    CREATE UNIQUE INDEX UX_Transactions_User_Idempotency ON dbo.Transactions(UserId, IdempotencyKey);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_UserId')
    CREATE INDEX IX_Transactions_UserId ON dbo.Transactions(UserId);
GO

-- Same per-user scoping for order idempotency keys: re-key the table on (CustomerId, Key).
IF COL_LENGTH('dbo.IdempotencyKeys', 'CustomerId') IS NULL
BEGIN
    ALTER TABLE dbo.IdempotencyKeys ADD CustomerId INT NULL;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.IdempotencyKeys') AND name = 'CustomerId' AND is_nullable = 1)
BEGIN
    UPDATE k SET CustomerId = o.CustomerId
    FROM dbo.IdempotencyKeys k JOIN dbo.Orders o ON o.Id = k.OrderId
    WHERE k.CustomerId IS NULL;

    DECLARE @pk SYSNAME = (SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.IdempotencyKeys') AND type = 'PK');
    IF @pk IS NOT NULL EXEC('ALTER TABLE dbo.IdempotencyKeys DROP CONSTRAINT ' + @pk);

    ALTER TABLE dbo.IdempotencyKeys ALTER COLUMN CustomerId INT NOT NULL;
    ALTER TABLE dbo.IdempotencyKeys ADD CONSTRAINT PK_IdempotencyKeys PRIMARY KEY (CustomerId, [Key]);
END
GO
