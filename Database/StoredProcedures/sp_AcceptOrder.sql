-- Concurrency: two riders tap "accept" on the same order at the same moment.
-- The conditional UPDATE (... AND Status = 'PENDING') is atomic: SQL Server makes the second
-- UPDATE wait for the first one's row lock, then re-checks the predicate. The order is no longer
-- PENDING, so zero rows change and that rider gets a 409 instead of silently overwriting the first.
-- UX_Orders_ActiveRider separately stops one rider holding two active orders.
CREATE OR ALTER PROCEDURE dbo.sp_AcceptOrder
    @OrderId INT,
    @RiderId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Rows INT;
    BEGIN TRY
        UPDATE dbo.Orders
        SET RiderId = @RiderId, Status = 'ACCEPTED', AcceptedAt = GETDATE()
        WHERE Id = @OrderId AND Status = 'PENDING';
        SET @Rows = @@ROWCOUNT;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 50409, 'RIDER_BUSY: You already have an active delivery. Complete it before accepting another.', 1;
        THROW;
    END CATCH

    IF @Rows = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId)
            THROW 50404, 'ORDER_NOT_FOUND: Order not found.', 1;
        THROW 50409, 'ORDER_NOT_AVAILABLE: This order has already been accepted or is no longer pending.', 1;
    END

    EXEC dbo.sp_GetOrderById @Id = @OrderId;
END
GO
