CREATE OR ALTER PROCEDURE dbo.sp_AdminStats
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM dbo.Users) AS totalUsers,
        (SELECT COUNT(*) FROM dbo.Orders) AS totalOrders,
        (SELECT COUNT(*) FROM dbo.Orders WHERE Status = 'PENDING') AS pendingOrders,
        (SELECT COUNT(*) FROM dbo.Orders WHERE Status = 'DELIVERED') AS deliveredOrders,
        (SELECT COUNT(*) FROM dbo.Orders WHERE Status = 'ACCEPTED') AS acceptedOrders,
        (SELECT ISNULL(SUM(Balance), 0) FROM dbo.Wallets) AS walletBalance,
        (SELECT COUNT(*) FROM dbo.IdempotencyKeys) AS idempotencyCount;
END
GO
