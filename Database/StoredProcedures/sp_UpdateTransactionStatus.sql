-- Step 2 of a withdrawal: settle a PENDING transaction as SUCCESS, or FAILED with a refund.
-- Only a PENDING transaction can change. Once it is settled, repeat calls (client retries, a
-- re-delivered webhook, the reconciliation job) are no-ops, so a refund can never be paid twice.
CREATE OR ALTER PROCEDURE dbo.sp_UpdateTransactionStatus
    @Reference         NVARCHAR(50),
    @NewStatus         NVARCHAR(20),
    @PaystackReference NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NewStatus NOT IN ('SUCCESS', 'FAILED')
        THROW 50400, 'INVALID_STATUS: Status must be SUCCESS or FAILED.', 1;

    DECLARE @Amount DECIMAL(18,2), @WalletId INT, @OldStatus NVARCHAR(20);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Amount = Amount, @WalletId = WalletId, @OldStatus = Status
        FROM dbo.Transactions WITH (UPDLOCK, ROWLOCK)
        WHERE Reference = @Reference;

        IF @OldStatus IS NULL
            THROW 50404, 'TRANSACTION_NOT_FOUND: Transaction not found.', 1;

        IF @OldStatus = 'PENDING'
        BEGIN
            IF @NewStatus = 'FAILED'
                UPDATE dbo.Wallets SET Balance = Balance + @Amount, UpdatedAt = GETDATE() WHERE Id = @WalletId;

            UPDATE dbo.Transactions
            SET Status = @NewStatus,
                PaystackReference = ISNULL(@PaystackReference, PaystackReference),
                UpdatedAt = GETDATE()
            WHERE Reference = @Reference;
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH

    SELECT Reference, Status, Amount, PaystackReference FROM dbo.Transactions WHERE Reference = @Reference;
END
GO
