-- Not exposed by the API yet: it should be called by a verified Paystack charge webhook.
-- The credit and its ledger entry commit together or not at all.
CREATE OR ALTER PROCEDURE dbo.sp_FundWallet
    @UserId INT,
    @Amount DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount IS NULL OR @Amount <= 0
        THROW 50400, 'INVALID_AMOUNT: Amount must be greater than zero.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @WalletId INT = (SELECT Id FROM dbo.Wallets WITH (UPDLOCK) WHERE UserId = @UserId);
        IF @WalletId IS NULL
            THROW 50404, 'WALLET_NOT_FOUND: Wallet not found.', 1;

        UPDATE dbo.Wallets SET Balance = Balance + @Amount, UpdatedAt = GETDATE() WHERE Id = @WalletId;

        INSERT INTO dbo.WalletTransactions (WalletId, Type, Amount, Reference, Description)
        VALUES (@WalletId, 'CREDIT', @Amount, 'FUND_' + LEFT(CONVERT(NVARCHAR(36), NEWID()), 8), 'Wallet funding');

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH

    SELECT Id, UserId, Balance, UpdatedAt FROM dbo.Wallets WHERE UserId = @UserId;
END
GO
