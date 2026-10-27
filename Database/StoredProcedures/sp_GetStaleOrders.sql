-- Used by the Hangfire job: orders nobody has accepted after @OlderThanMinutes.
CREATE OR ALTER PROCEDURE dbo.sp_GetStaleOrders
    @OlderThanMinutes INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Reference, CreatedAt
    FROM dbo.Orders
    WHERE Status = 'PENDING' AND CreatedAt < DATEADD(MINUTE, -@OlderThanMinutes, GETDATE());
END
GO
