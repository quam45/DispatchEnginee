-- Only the assigned rider can complete an ACCEPTED order, and only with the customer's code.
-- All rules sit in one atomic UPDATE; the follow-up SELECT only works out which error to report.
CREATE OR ALTER PROCEDURE dbo.sp_CompleteOrderWithOtp
    @OrderId INT,
    @RiderId INT,
    @Otp     NVARCHAR(6)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Orders
    SET Status = 'DELIVERED', DeliveredAt = GETDATE()
    WHERE Id = @OrderId AND RiderId = @RiderId AND Status = 'ACCEPTED' AND DeliveryOtp = @Otp;

    IF @@ROWCOUNT = 0
    BEGIN
        DECLARE @Found BIT = 0, @Status NVARCHAR(20), @AssignedRider INT;
        SELECT @Found = 1, @Status = Status, @AssignedRider = RiderId FROM dbo.Orders WHERE Id = @OrderId;

        IF @Found = 0
            THROW 50404, 'ORDER_NOT_FOUND: Order not found.', 1;
        IF @AssignedRider IS NULL OR @AssignedRider <> @RiderId
            THROW 50403, 'NOT_YOUR_ORDER: This order is not assigned to you.', 1;
        IF @Status <> 'ACCEPTED'
            THROW 50409, 'INVALID_STATUS: Only an ACCEPTED order can be completed.', 1;
        THROW 50400, 'INVALID_OTP: The delivery code is incorrect.', 1;
    END

    EXEC dbo.sp_GetOrderById @Id = @OrderId;
END
GO
