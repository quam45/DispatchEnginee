-- Creates an order. With @IdempotencyKey, a retry of the same request returns the original order
-- instead of creating a second one. The key check and the insert are atomic, unlike the old
-- check-then-save pair of procedures, which two simultaneous retries could both slip through.
CREATE OR ALTER PROCEDURE dbo.sp_CreateOrder
    @Reference      NVARCHAR(20),
    @CustomerId     INT,
    @PickupLat      FLOAT,
    @PickupLng      FLOAT,
    @PickupAddress  NVARCHAR(255),
    @DropoffLat     FLOAT,
    @DropoffLng     FLOAT,
    @DropoffAddress NVARCHAR(255),
    @Price          DECIMAL(18,2),
    @DistanceKm     FLOAT,
    @DeliveryOtp    NVARCHAR(6),
    @IdempotencyKey NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OrderId INT;

    IF @IdempotencyKey IS NOT NULL
        SELECT @OrderId = OrderId FROM dbo.IdempotencyKeys
        WHERE CustomerId = @CustomerId AND [Key] = @IdempotencyKey;

    IF @OrderId IS NULL
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;

            INSERT INTO dbo.Orders (Reference, CustomerId, Status, PickupLat, PickupLng, PickupAddress,
                                    DropoffLat, DropoffLng, DropoffAddress, Price, DistanceKm, DeliveryOtp)
            VALUES (@Reference, @CustomerId, 'PENDING', @PickupLat, @PickupLng, @PickupAddress,
                    @DropoffLat, @DropoffLng, @DropoffAddress, @Price, @DistanceKm, @DeliveryOtp);
            SET @OrderId = SCOPE_IDENTITY();

            IF @IdempotencyKey IS NOT NULL
                INSERT INTO dbo.IdempotencyKeys (CustomerId, [Key], OrderId)
                VALUES (@CustomerId, @IdempotencyKey, @OrderId);

            COMMIT;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            -- A concurrent request with the same key committed first. Its key row made our insert fail
            -- (and rolled back our order), so hand back the winner's order.
            IF ERROR_NUMBER() NOT IN (2601, 2627) OR @IdempotencyKey IS NULL THROW;

            SET @OrderId = (SELECT OrderId FROM dbo.IdempotencyKeys
                            WHERE CustomerId = @CustomerId AND [Key] = @IdempotencyKey);
            IF @OrderId IS NULL THROW;
        END CATCH
    END

    EXEC dbo.sp_GetOrderById @Id = @OrderId;
END
GO
