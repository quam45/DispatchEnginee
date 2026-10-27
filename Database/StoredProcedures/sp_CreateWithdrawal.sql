-- Step 1 of a withdrawal: debit (hold) the money and record a PENDING transaction.
-- The API then calls Paystack and settles the transaction with sp_UpdateTransactionStatus.
--
-- Idempotency: the same user + idempotency key always maps to the same withdrawal, so a retry
--   never debits twice. Reusing a key for a different amount is rejected.
-- Concurrency: UPDLOCK on the wallet row queues concurrent withdrawals, so two requests can't both
--   pass the balance check. CK_Wallets_Balance is the final backstop against going negative.
-- Returns one row: Reference, Status, Amount, IsReplay.
CREATE OR ALTER PROCEDURE dbo.sp_CreateWithdrawal
    @UserId         INT,
    @Amount         DECIMAL(18,2),
    @IdempotencyKey NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Without this, a negative amount would *credit* the wallet
    IF @Amount IS NULL OR @Amount <= 0
        THROW 50400, 'INVALID_AMOUNT: Amount must be greater than zero.', 1;

    DECLARE @Reference NVARCHAR(50), @Status NVARCHAR(20), @ExistingAmount DECIMAL(18,2), @IsReplay BIT = 0;
    DECLARE @WalletId INT, @Balance DECIMAL(18,2);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Reference = Reference, @Status = Status, @ExistingAmount = Amount
        FROM dbo.Transactions
        WHERE UserId = @UserId AND IdempotencyKey = @IdempotencyKey;

        IF @Reference IS NOT NULL
            SET @IsReplay = 1;
        ELSE
        BEGIN
            SELECT @WalletId = Id, @Balance = Balance
            FROM dbo.Wallets WITH (UPDLOCK, ROWLOCK)
            WHERE UserId = @UserId;

            IF @WalletId IS NULL
                THROW 50404, 'WALLET_NOT_FOUND: Wallet not found.', 1;
            IF ISNULL(@Balance, 0) < @Amount
                THROW 50400, 'INSUFFICIENT_BALANCE: Your wallet balance is too low for this withdrawal.', 1;

            UPDATE dbo.Wallets SET Balance = Balance - @Amount, UpdatedAt = GETDATE() WHERE Id = @WalletId;

            SET @Reference = 'TRX_' + LEFT(CONVERT(NVARCHAR(36), NEWID()), 8);
            SET @Status = 'PENDING';

            INSERT INTO dbo.Transactions (Reference, UserId, WalletId, Amount, Type, Status, IdempotencyKey)
            VALUES (@Reference, @UserId, @WalletId, @Amount, 'WITHDRAWAL', 'PENDING', @IdempotencyKey);
        END

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        -- Two simultaneous requests with the same key: both missed the lookup above, but the unique
        -- index (UserId, IdempotencyKey) let only one insert through. The rollback undid our debit;
        -- return the winner's transaction.
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;

        SELECT @Reference = Reference, @Status = Status, @ExistingAmount = Amount
        FROM dbo.Transactions
        WHERE UserId = @UserId AND IdempotencyKey = @IdempotencyKey;
        IF @Reference IS NULL THROW;
        SET @IsReplay = 1;
    END CATCH

    IF @IsReplay = 1 AND @ExistingAmount <> @Amount
        THROW 50422, 'IDEMPOTENCY_KEY_REUSED: This idempotency key was already used for a different amount.', 1;

    SELECT @Reference AS Reference, @Status AS Status, @Amount AS Amount, @IsReplay AS IsReplay;
END
GO
