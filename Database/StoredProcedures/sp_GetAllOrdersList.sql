-- Paged, optionally filtered order list. Result set 1 = total count, result set 2 = the page.
-- Never returns DeliveryOtp: list views don't need it.
-- OPTION (RECOMPILE) gives each filter combination its own plan instead of one cached plan
-- that suits only the first set of parameters it was compiled for.
CREATE OR ALTER PROCEDURE dbo.sp_GetAllOrdersList
    @CustomerId INT = NULL,
    @RiderId    INT = NULL,
    @Status     NVARCHAR(20) = NULL,
    @Page       INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1 SET @Page = 1;
    IF @PageSize NOT BETWEEN 1 AND 100 SET @PageSize = 20;

    SELECT COUNT(*) AS TotalCount
    FROM dbo.Orders AS o
    WHERE (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
      AND (@RiderId IS NULL OR o.RiderId = @RiderId)
      AND (@Status IS NULL OR o.Status = @Status)
    OPTION (RECOMPILE);

    SELECT o.Id, o.Reference, o.CustomerId, o.RiderId, o.Status,
           o.PickupLat, o.PickupLng, o.PickupAddress, o.DropoffLat, o.DropoffLng, o.DropoffAddress,
           o.Price, o.DistanceKm, o.CreatedAt, o.AcceptedAt, o.DeliveredAt,
           u.Name AS CustomerName
    FROM dbo.Orders AS o
    LEFT JOIN dbo.Users AS u ON u.Id = o.CustomerId
    WHERE (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
      AND (@RiderId IS NULL OR o.RiderId = @RiderId)
      AND (@Status IS NULL OR o.Status = @Status)
    ORDER BY o.CreatedAt DESC, o.Id DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
