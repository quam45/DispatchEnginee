-- Idempotency keys for POST /api/Orders (header: X-Idempotency-Key)
IF OBJECT_ID('IdempotencyKeys') IS NULL
CREATE TABLE IdempotencyKeys (
    [Key] NVARCHAR(100) NOT NULL PRIMARY KEY,
    OrderId INT NOT NULL REFERENCES Orders(Id),
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE OR ALTER PROCEDURE sp_CheckIdempotency @Key NVARCHAR(100) AS
BEGIN
    SELECT OrderId FROM IdempotencyKeys WHERE [Key] = @Key;
END
GO

CREATE OR ALTER PROCEDURE sp_SaveIdempotency @Key NVARCHAR(100), @OrderId INT AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM IdempotencyKeys WHERE [Key] = @Key)
        INSERT INTO IdempotencyKeys ([Key], OrderId) VALUES (@Key, @OrderId);
END
GO
