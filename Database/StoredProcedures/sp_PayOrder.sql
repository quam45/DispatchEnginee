-- Not exposed by the API yet. Pays for an order from the customer's wallet, at most once.
-- UPDLOCK on the order serialises concurrent pay attempts for the same order, so the
-- "already paid?" check can't be raced; UPDLOCK on the wallet does the same for the balance check.
CREATE OR ALTER PROCEDURE dbo.sp_PayOrder
    @OrderId INT,
    @UserId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Price DECIMAL(18,2), @Ref NVARCHAR(20), @CustomerId INT, @WalletId INT, @Balance DECIMAL(18,2);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Price = Price, @Ref = Reference, @CustomerId = CustomerId
        FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId;

        IF @Ref IS NULL
            THROW 50404, 'ORDER_NOT_FOUND: Order not found.', 1;
        IF @CustomerId <> @UserId
            THROW 50403, 'NOT_YOUR_ORDER: You can only pay for your own orders.', 1;
        IF EXISTS (SELECT 1 FROM dbo.WalletTransactions WHERE Reference = 'PAY_' + @Ref)
            THROW 50409, 'ORDER_ALREADY_PAID: This order has already been paid for.', 1;

        SELECT @WalletId = Id, @Balance = Balance FROM dbo.Wallets WITH (UPDLOCK, ROWLOCK) WHERE UserId = @UserId;
        IF ISNULL(@Balance, 0) < @Price
            THROW 50400, 'INSUFFICIENT_BALANCE: Your wallet balance is too low to pay for this order.', 1;

        UPDATE dbo.Wallets SET Balance = Balance - @Price, UpdatedAt = GETDATE() WHERE Id = @WalletId;

        INSERT INTO dbo.WalletTransactions (WalletId, Type, Amount, Reference, Description)
        VALUES (@WalletId, 'DEBIT', @Price, 'PAY_' + @Ref, 'Payment for ' + @Ref);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH

    SELECT @Price AS PaidAmount, @Ref AS OrderRef;
END
GO
