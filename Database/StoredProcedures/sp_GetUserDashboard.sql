-- Four result sets in one round trip; read with Dapper's QueryMultipleAsync.
CREATE OR ALTER PROCEDURE dbo.sp_GetUserDashboard
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Phone, Email, Role FROM dbo.Users WHERE Id = @UserId;

    SELECT Id, UserId, Balance, UpdatedAt FROM dbo.Wallets WHERE UserId = @UserId;

    SELECT COUNT(*) AS TotalOrders,
           ISNULL(SUM(CASE WHEN Status = 'DELIVERED' THEN Price END), 0) AS TotalSpent
    FROM dbo.Orders
    WHERE CustomerId = @UserId;

    SELECT TOP (5)
        Id, Reference, CustomerId, RiderId, Status,
        PickupLat, PickupLng, PickupAddress, DropoffLat, DropoffLng, DropoffAddress,
        Price, DistanceKm, CreatedAt, AcceptedAt, DeliveredAt
    FROM dbo.Orders
    WHERE CustomerId = @UserId
    ORDER BY CreatedAt DESC, Id DESC;
END
GO
