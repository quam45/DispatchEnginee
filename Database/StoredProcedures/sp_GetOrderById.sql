-- Includes DeliveryOtp; the API only shows it to the order's customer.
CREATE OR ALTER PROCEDURE dbo.sp_GetOrderById
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.Id, o.Reference, o.CustomerId, o.RiderId, o.Status,
           o.PickupLat, o.PickupLng, o.PickupAddress, o.DropoffLat, o.DropoffLng, o.DropoffAddress,
           o.Price, o.DistanceKm, o.DeliveryOtp, o.CreatedAt, o.AcceptedAt, o.DeliveredAt,
           u.Name AS CustomerName
    FROM dbo.Orders AS o
    LEFT JOIN dbo.Users AS u ON u.Id = o.CustomerId
    WHERE o.Id = @Id;
END
GO
